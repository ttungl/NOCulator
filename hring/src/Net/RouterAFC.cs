//#define DEBUG
//#define PREMPT



using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public class AFCBufferSlot : IComparable
    {
        Flit m_f;
        
        public Flit flit { get { return m_f; } set { m_f = value; } }

        public AFCBufferSlot(Flit f)
        {
            m_f = f;
        }

        public int CompareTo(object o)
        {
			if (o is AFCBufferSlot) {
				if (Config.slowdown_aware == false)
					return Router_Flit_OldestFirst._rank (m_f, (o as AFCBufferSlot).m_f);
				else
					return Router_Flit_SlowDown._rank (m_f, (o as AFCBufferSlot).m_f);
			}
            else
                throw new ArgumentException("bad comparison");
        }

    }

    public class AFCUtilAvg
    {
        double m_avg;
        double m_window_sum;
        double[] m_window;
        int m_window_ptr;

        public AFCUtilAvg()
        {
            m_window = new double[Config.afc_avg_window];
            m_window_ptr = 0;
            m_window_sum = 0;
            m_avg = 0;
        }

        public void Add(double util)
        {
            // add new sample to window and update sum
            m_window_sum -= m_window[m_window_ptr];
            m_window[m_window_ptr] = util;
            m_window_sum += util;
            m_window_ptr = (m_window_ptr + 1) % Config.afc_avg_window;

            // mix window-average into EWMA
            m_avg = Config.afc_ewma_history * m_avg +
                (1 - Config.afc_ewma_history) * (m_window_sum / Config.afc_avg_window);
        }

        public double Avg { get { return m_avg; } }
    }

    public class Router_AFC : Router
    {
        // injectSlot is from Node
        protected Flit m_injectSlot;
        
        // buffers, indexed by physical channel and virtual network
        protected MinHeap<AFCBufferSlot>[,] m_buf;
        int m_buf_occupancy;
		int [,] m_arb_trial;  // counter to register the cycles the flit sits at the front of each VC.


		//check priority inversion
		private void prior_inv_check (int pc, int vnet)
		{
			if (m_buf [pc, vnet].Count > 1) { // has more than 1 flit
				AFCBufferSlot top = m_buf [pc, vnet].Peek ();
				for (int i = 2; i <= m_buf [pc, vnet].Count; i++)
				{
					AFCBufferSlot flit = m_buf [pc, vnet].Peek (i);
					if (flit.CompareTo(top) < 0) 
					{
						Simulator.stats.priority_inv.Add(1);
						break;
					}
				}

			}
		}


        // buffers active?
        protected bool m_buffered;

        public Router_AFC(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;

            m_buf = new MinHeap<AFCBufferSlot>[5, Config.afc_vnets];
			m_arb_trial = new int[5, Config.afc_vnets];
            for (int pc = 0; pc < 5; pc++)
                for (int i = 0; i < Config.afc_vnets; i++)
			{
                m_buf[pc, i] = new MinHeap<AFCBufferSlot>();
				m_arb_trial[pc, i] = 0;
			}

            m_buffered = false;
            m_buf_occupancy = 0;
        }


        protected Router_AFC getNeigh(int dir)
        {
            return neigh[dir] as Router_AFC;
        }

        // accept one ejected flit into rxbuf
        protected void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
			if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
				statsEjectPacket (f.packet);
            m_n.receiveFlit(f);
        }

        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
			int flitsTryToEject = 0;
			for (int dir = 0; dir < 4; dir++)
				if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.dest.ID == ID)
					flitsTryToEject ++;
			Simulator.stats.flitsTryToEject[flitsTryToEject].Add();

            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.state != Flit.State.Placeholder &&
                        linkIn[dir].Out.dest.ID == ID &&
                        (ret == null || rank(linkIn[dir].Out, ret) < 0))
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                }

            if (bestDir != -1) linkIn[bestDir].Out = null;
            return ret;
        }

        // keep these as member vars so we don't have to allocate on every step
        // (why can't we have arrays on the stack like in C?)
        Flit[] input = new Flit[4]; 
        AFCBufferSlot[] requesters = new AFCBufferSlot[5];
        int[] requester_dir = new int[5];

        Queue<AFCBufferSlot> m_freeAFCSlots = new Queue<AFCBufferSlot>();

        AFCBufferSlot getFreeBufferSlot(Flit f)
        {
            if (m_freeAFCSlots.Count > 0)
            {
                AFCBufferSlot s = m_freeAFCSlots.Dequeue();
                s.flit = f;
                return s;
            }
            else
                return new AFCBufferSlot(f);
        }
        void returnFreeBufferSlot(AFCBufferSlot s)
        {
            m_freeAFCSlots.Enqueue(s);
        }

        void switchBufferless()
        {
            m_buffered = false;
        }

        void switchBuffered()
        {
            m_buffered = true;
            if (m_injectSlot != null)
            {
                InjectFlit(m_injectSlot);
                m_injectSlot = null;
            }
        }

        AFCUtilAvg m_util_avg = new AFCUtilAvg();

        protected override void _doStep()
        {
            int flit_count = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                    flit_count++;

            m_util_avg.Add((double)flit_count / neighbors);

            Simulator.stats.afc_avg.Add(m_util_avg.Avg);
            Simulator.stats.afc_avg_bysrc[ID].Add(m_util_avg.Avg);

            bool old_status = m_buffered;
            bool new_status = old_status;
            bool gossip_induced = false;

            if (Config.afc_force)
            {
                new_status = Config.afc_force_buffered;
            }
            else
            {
                if (!m_buffered && (m_util_avg.Avg > Config.afc_buf_threshold))
                    new_status = true;

                if (m_buffered && (m_util_avg.Avg < Config.afc_bless_threshold) && m_buf_occupancy == 0)
                    new_status = false;

                // check at least one free slot in downstream routers; if not, gossip-induced switch
                for (int n = 0; n < 4; n++)
                {
                    Router_AFC nr = getNeigh(n);
                    if (nr == null) continue;
                    int oppDir = (n + 2) % 4;
                    for (int vnet = 0; vnet < Config.afc_vnets; vnet++)
                    {
                        int occupancy = nr.m_buf[oppDir, vnet].Count;
                        if ((capacity(vnet) - occupancy) < 2)
                        {
                            gossip_induced = true;
                            break;
                        }
                    }
                }
                if (gossip_induced) new_status = true;
            }

            // perform switching and stats accumulation
            if (old_status && !new_status)
            {
                switchBufferless();
                Simulator.stats.afc_switch.Add();
                Simulator.stats.afc_switch_bless.Add();
                Simulator.stats.afc_switch_bysrc[ID].Add();
                Simulator.stats.afc_switch_bless_bysrc[ID].Add();
            }
            if (!old_status && new_status)
            {
                switchBuffered();
                Simulator.stats.afc_switch.Add();
                Simulator.stats.afc_switch_buf.Add();
                Simulator.stats.afc_switch_bysrc[ID].Add();
                Simulator.stats.afc_switch_buf_bysrc[ID].Add();
            }

            if (m_buffered)
            {
                Simulator.stats.afc_buffered.Add();
                Simulator.stats.afc_buffered_bysrc[ID].Add();
                if (gossip_induced)
                {
                    Simulator.stats.afc_gossip.Add();
                    Simulator.stats.afc_gossip_bysrc[ID].Add();
                }
            }
            else
            {
                Simulator.stats.afc_bless.Add();
                Simulator.stats.afc_bless_bysrc[ID].Add();
            }

            if (m_buffered)
            {
                Simulator.stats.afc_buf_enabled.Add();
                Simulator.stats.afc_buf_enabled_bysrc[ID].Add();

                Simulator.stats.afc_buf_occupancy.Add(m_buf_occupancy);
                Simulator.stats.afc_buf_occupancy_bysrc[ID].Add(m_buf_occupancy);

                // grab inputs into buffers
                for (int dir = 0; dir < 4; dir++)
                {
                    if (linkIn[dir] != null && linkIn[dir].Out != null)
                    {
                        Flit f = linkIn[dir].Out;
                        linkIn[dir].Out = null;
                        AFCBufferSlot slot = getFreeBufferSlot(f);
						f.enterBuffer = Simulator.CurrentRound;
                        m_buf[dir, f.packet.getClass()].Enqueue(slot);
                        m_buf_occupancy++;

                        Simulator.stats.afc_buf_write.Add();
                        Simulator.stats.afc_buf_write_bysrc[ID].Add();
                    }
                }

                // perform arbitration: (i) collect heads of each virtual-net
                // heap (which represents many VCs) to obtain a single requester
                // per physical channel; (ii)  request outputs among these
                // requesters based on DOR; (iii) select a single winner
                // per output

                for (int i = 0; i < 5; i++)
                {
                    requesters[i] = null;
                    requester_dir[i] = -1;
                }
                
                // find the highest-priority vnet head for each input PC
				for (int pc = 0; pc < 5; pc++) {

					ulong causeIntf = 0;
					for (int vnet = 0; vnet < Config.afc_vnets; vnet++)
						if (m_buf [pc, vnet].Count > 0) {
							AFCBufferSlot top = m_buf [pc, vnet].Peek ();
			
							PreferredDirection pd = determineDirection (top.flit, coord);
							int outdir = (pd.xDir != Simulator.DIR_NONE) ?
                                pd.xDir : pd.yDir;

							if (outdir == Simulator.DIR_NONE)
								outdir = 4; // local ejection

							/* profile begin
							 * Enable to profile priority inversition and false blocking.
							 * False blocking occurs when the upstream router cannot send flit to the downstream router when the required VC is full, however,
							 * the downstream router still have available slot.
							// skip if (i) not local ejection and (ii)
							// destination router is buffered and (iii)
							// no credits left to destination router
							bool correct_block = true; // it is true when all the downstream VCs are full.
							if (outdir != 4) {
								Router_AFC nrouter = (Router_AFC)neigh [outdir];
								int ndir = (outdir + 2) % 4;
								if (nrouter.m_buf [ndir, vnet].Count >= capacity (vnet) && nrouter.m_buffered)
								{
								// Let's check the occurance when the other VC has empty slot whereas the required VC is full
									m_arb_trial[pc, vnet] ++;
									prior_inv_check (pc, vnet);
									for (int vnet_neigh = 0; vnet_neigh < Config.afc_vnets; vnet_neigh++)
										if (nrouter.m_buf[ndir,vnet_neigh].Count < capacity (vnet) &&
										    nrouter.m_buffered)
										{
											Simulator.stats.false_block.Add(1);
											correct_block = false;
											break;
										}
									if (correct_block)
										Simulator.stats.correct_block.Add(1);
									continue;
								}
							}
							profile end */

							// otherwise, contend for top requester from this
							// physical channel
							
							if (requesters [pc] == null || top.CompareTo (requesters [pc]) < 0) {
								//    Check Interference at virtual channel arbitration
								if (requesters [pc] != null) {
									if (top.flit.packet.requesterID != requesters [pc].flit.packet.requesterID) {
										if (requesters [pc].flit.packet.critical) // only log interferenceCycle for critical packet, but still log causeIntf
											requesters [pc].flit.intfCycle++;
										causeIntf++;

										#if DEBUG
										//Console.WriteLine ("BLOCK Req_addr = {1}, Node {2}, intfCycle = {3}, time = {0}", Simulator.CurrentRound, requesters [pc].flit.packet.txn.req_addr, ID, requesters [pc].flit.packet.txn.interferenceCycle);
										#endif
									}
									//prior_inv_check (pc, requesters [pc].flit.packet.getClass());
									//m_arb_trial[pc, requesters [pc].flit.packet.getClass()] ++;
								}
									
								requesters [pc] = top;
								requester_dir [pc] = outdir;
							} 
							
							else if (requesters [pc] != null && top.CompareTo (requesters [pc]) > 0) {
								if (top.flit.packet.requesterID != requesters [pc].flit.packet.requesterID) {
									if (top.flit.packet.critical) // only log interferenceCycle for critical packet, but still log causeIntf
										top.flit.intfCycle++;
									causeIntf++;
								}
								//m_arb_trial[pc, top.flit.packet.getClass()] ++;
								//prior_inv_check (pc, top.flit.packet.getClass());
							}

						}
					if (requesters[pc] != null && causeIntf != 0)
						requesters[pc].flit.packet.txn.causeIntf = requesters[pc].flit.packet.txn.causeIntf + causeIntf;
				}

                // find the highest-priority requester for each output, and pop
                // it from its heap

                for (int outdir = 0; outdir < 5; outdir++)
                {
                    AFCBufferSlot top = null;
					AFCBufferSlot top2 = null;
					int flitsTryToEject = 0;
                    int top_indir = -1;  // the channel
					int top_indir2 = -1;
					ulong causeIntf = 0;
                    for (int req = 0; req < 5; req++)
                        if (requesters[req] != null &&
                                requester_dir[req] == outdir)
                        {
							if (outdir == 4) flitsTryToEject ++;
							if (top == null || requesters [req].CompareTo (top) < 0) {
								//	  Check Interference at switch arbritration
								//    here "top" is the flit being stalled.
								if (top != null) {
									if (requesters [req].flit.packet.requesterID != top.flit.packet.requesterID) {
										if (top.flit.packet.critical)
											top.flit.intfCycle++;
										causeIntf++;
										#if DEBUG
										//Console.WriteLine ("BLOCK Req_addr = {1}, Node {2}, intfCycle = {3}, time = {0}", Simulator.CurrentRound, top.flit.packet.txn.req_addr, ID, top.flit.packet.txn.interferenceCycle);
										#endif
									}
									//m_arb_trial[req, top.flit.packet.getClass()] ++;
									//prior_inv_check (req, top.flit.packet.getClass());
								}

								// end Xiyue
								top = requesters [req];
								top_indir = req;
							}
							
							else if (top != null && requesters [req].CompareTo (top) > 0) {
								if (requesters [req].flit.packet.requesterID != top.flit.packet.requesterID) {
									if (requesters [req].flit.packet.critical)
										requesters [req].flit.intfCycle++;		
									causeIntf++;
								}
								//m_arb_trial[req, requesters [req].flit.packet.getClass()] ++;
								//prior_inv_check (req, requesters [req].flit.packet.getClass());
							}
                        }
					
					if (top != null && causeIntf != 0)
						top.flit.packet.txn.causeIntf = top.flit.packet.txn.causeIntf + causeIntf;
					if (outdir == 4)
						Simulator.stats.flitsTryToEject[flitsTryToEject].Add();
					/*
					 * Support dual ejection
					 */
					if (Config.meshEjectTrial == 2 && outdir == 4 && top_indir != -1) // ejectTwice
						for (int req = 0; req < 5; req ++)
							if (requesters[req] != null && requester_dir[req] == outdir && req != top_indir)
								if (top2 == null)
								{
									top2 = requesters[req];
									top_indir2 = req;
								}					
					if (top_indir != -1 && top_indir2 != -1)
					{
						if (top.flit.packet == top2.flit.packet)
							Simulator.stats.ejectsFromSamePacket.Add(1);
						else 
							Simulator.stats.ejectsFromSamePacket.Add(0);
					}

					// Put flit on the output channels
                    if (top_indir != -1)
                    {
                        m_buf[top_indir, top.flit.packet.getClass()].Dequeue();
						//m_arb_trial[top_indir, top.flit.packet.getClass()] = 0;
						if (top.flit.enterBuffer == Simulator.CurrentRound)
							Simulator.stats.afc_bufferBypass.Add(1);
                        Simulator.stats.afc_buf_read.Add();
                        Simulator.stats.afc_buf_read_bysrc[ID].Add();
                        Simulator.stats.afc_xbar.Add();
                        Simulator.stats.afc_xbar_bysrc[ID].Add();
				
                        if (top_indir == 4)
                            statsInjectFlit(top.flit);
			
                        // propagate to next router (or eject)
                        if (outdir == 4)
                            acceptFlit(top.flit);
                        else
                            linkOut[outdir].In = top.flit;
												
                        returnFreeBufferSlot(top);
                        m_buf_occupancy--;
                    }
					// for dual ejection
					if (outdir == 4 && top_indir2 != -1)
					{
						m_buf[top_indir2, top2.flit.packet.getClass()].Dequeue();
						if (top2.flit.enterBuffer == Simulator.CurrentRound)
							Simulator.stats.afc_bufferBypass.Add(1);
						acceptFlit(top2.flit);
					}
                }

				// remove a flit from head after N trials.
				/* 
				if (Config.preempt)
					for (int pc = 0; pc < 5; pc++) {
						for (int vnet = 0; vnet < Config.afc_vnets; vnet++)
							if (m_buf [pc, vnet].Count > 1) {  // do it only when the front flit is blocking some other flits.
								if (m_arb_trial[pc,vnet] >= Config.preempt_threshold) 
								{
									AFCBufferSlot top = m_buf[pc, vnet].Dequeue();
									m_buf[pc, vnet].Enqueue(top);
									m_arb_trial[pc,vnet] = 0;
								}
							}
					}
				*/
            }
            else // by Xiyue: for not buffered.
            {
				for (int i = 0; i < Config.meshEjectTrial; i++)
                {
					Flit eject = ejectLocal();
                	if (eject != null)
                    	acceptFlit(eject);
				}

                for (int i = 0; i < 4; i++) input[i] = null;

                // grab inputs into a local array so we can sort
                int c = 0;
                for (int dir = 0; dir < 4; dir++)
                    if (linkIn[dir] != null && linkIn[dir].Out != null)
                    {
                        input[c++] = linkIn[dir].Out;
                        linkIn[dir].Out.inDir = dir;
                        linkIn[dir].Out = null;
                    }

                // sometimes network-meddling such as flit-injection can put unexpected
                // things in outlinks...
                int outCount = 0;
                for (int dir = 0; dir < 4; dir++)
                    if (linkOut[dir] != null && linkOut[dir].In != null)
                        outCount++;

                bool wantToInject = m_injectSlot != null;
                bool canInject = (c + outCount) < neighbors;
                bool starved = wantToInject && !canInject;

                if (starved)
                {
                    Flit starvedFlit = m_injectSlot;
                    Simulator.controller.reportStarve(coord.ID);
                    statsStarve(starvedFlit);
                }
                if (canInject && wantToInject)
                {
                    Flit inj = null;
                    if (m_injectSlot != null)
                    {
                        inj = m_injectSlot;
                        m_injectSlot = null;
                    }
                    else
                        throw new Exception("trying to inject a null flit");

                    input[c++] = inj;

                    statsInjectFlit(inj);
                }



                // inline bubble sort is faster for this size than Array.Sort()
                // sort input[] by descending priority. rank(a,b) < 0 iff a has higher priority.
                for (int i = 0; i < 4; i++)
                    for (int j = i + 1; j < 4; j++)
                        if (input[j] != null &&
                                (input[i] == null ||
                                 rank(input[j], input[i]) < 0))
                        {
                            Flit t = input[i];
                            input[i] = input[j];
                            input[j] = t;
                        }

                // assign outputs
                for (int i = 0; i < 4 && input[i] != null; i++)
                {
                    PreferredDirection pd = determineDirection(input[i], coord);
                    int outDir = -1;

                    if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                    {
                        linkOut[pd.xDir].In = input[i];
                        outDir = pd.xDir;
                    }

                    else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    {
                        linkOut[pd.yDir].In = input[i];
                        outDir = pd.yDir;
                    }

                    // deflect!
                    else
                    {
                        input[i].Deflected = true;
                        int dir = 0;
                        if (Config.randomize_defl) dir = Simulator.rand.Next(4); // randomize deflection dir (so no bias)
                        for (int count = 0; count < 4; count++, dir = (dir + 1) % 4)
                            if (linkOut[dir] != null && linkOut[dir].In == null)
                            {
                                linkOut[dir].In = input[i];
                                outDir = dir;
                                break;
                            }

                        if (outDir == -1) throw new Exception(
                                String.Format("Ran out of outlinks in arbitration at node {0} on input {1} cycle {2} flit {3} c {4} neighbors {5} outcount {6}", coord, i, Simulator.CurrentRound, input[i], c, neighbors, outCount));
                    }
                }
            } //by Xiyue: end not buffered
        }

        public override bool canInjectFlit(Flit f)
        {
            int cl = f.packet.getClass();

            if (m_buffered)
                return m_buf[4, cl].Count < capacity(cl);
            else
                return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            Simulator.stats.afc_vnet[f.packet.getClass()].Add();

            if (m_buffered)
            {
                AFCBufferSlot slot = getFreeBufferSlot(f);
				f.enterBuffer = Simulator.CurrentRound;

				int temp_queueCycle;
				if (slot.flit.isTailFlit && slot.flit.packet.critical) {
					temp_queueCycle = (int)(Simulator.CurrentRound - slot.flit.packet.creationTime); 
					if (temp_queueCycle >= 0 && slot.flit.dest.ID != ID)
					{
						slot.flit.packet.txn.serialization_latency = slot.flit.packet.txn.serialization_latency + (ulong)slot.flit.packet.nrOfFlits;
						slot.flit.packet.txn.queue_latency = slot.flit.packet.txn.queue_latency + (ulong)Math.Max(temp_queueCycle,slot.flit.packet.nrOfFlits);

#if DEBUG
					
						Console.WriteLine ("!!!!!!BLOCK pkt = {4}, txn req_addr = {5}, serilatency = {0}, queue_latency = {1}, at time = {2} node {3}",
							slot.flit.packet.txn.serialization_latency, slot.flit.packet.txn.queue_latency, Simulator.CurrentRound, ID, slot.flit.packet.ID, slot.flit.packet.txn.req_addr);
#endif

						if (slot.flit.packet.txn.queue_latency < slot.flit.packet.txn.serialization_latency)
							throw new Exception("queue cycle less than serialization latency!");

					}else if (temp_queueCycle < 0)
						throw new Exception("queue latency is less than 0!");
				}

				m_buf[4, f.packet.getClass()].Enqueue(slot);
                m_buf_occupancy++;

                Simulator.stats.afc_buf_write.Add();
                Simulator.stats.afc_buf_write_bysrc[ID].Add();
            }
            else
            {
                if (m_injectSlot != null)
                    throw new Exception("Trying to inject twice in one cycle");

                m_injectSlot = f;
            }
        }

        int capacity(int cl)
        {
            // in the future, we might size each virtual network differently; for now,
            // we use just one virtual network (since there is no receiver backpressure)
            return Config.afc_buf_per_vnet;
        }

        public override void flush()
        {
            m_injectSlot = null;
        }

        protected virtual bool needFlush(Flit f) { return false; }
    }
}
