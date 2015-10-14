//#define PACKETDUMP
//#define PROMISEDUMP

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    /*
      A node can contain the following:

      * CPU and CoherentCache (work as a unit)

      * coherence directory
      * shared cache (only allowed if dir present, and home node must match)

      * memory controller


      The cache hierarchy is modeled as two levels, the coherent level
      (L1 and, in private-cache systems, L2) and the (optional) shared
      cache, on top of a collection of memory controllers. Coherence
      happens between coherent-level caches, and misses in the
      coherent caches go either to the shared cache (if configured) or
      directly to memory. The system is thus flexible enough to
      support both private and shared cache designs.
    */

    public class Node
    {
        Coord m_coord;
        NodeMapping m_mapping;

        public Coord coord { get { return m_coord; } }
        public NodeMapping mapping { get { return m_mapping; } }
        public Router router { get { return m_router; } }
        public MemCtlr mem { get { return m_mem; } }

        public CPU cpu { get { return m_cpu; } }
        private CPU m_cpu;
        private MemCtlr m_mem;

        private Router m_router;

        private IPrioPktPool m_inj_pool;
        private Queue<Flit> m_injQueue_flit, m_injQueue_evict;
		private Queue<Flit> [] m_injQueue_multi_flit;
        public Queue<Packet> m_local;

        public int RequestQueueLen { get { return m_inj_pool.FlitCount + m_injQueue_flit.Count; } }

        RxBufNaive m_rxbuf_naive;

        public Node(NodeMapping mapping, Coord c)
        {
            m_coord = c;
            m_mapping = mapping;

            if (mapping.hasCPU(c.ID))
            {
                m_cpu = new CPU(this);
            }
            if (mapping.hasMem(c.ID))
            {
				Console.WriteLine("Proc/Node.cs : MC locations:{0}", c.ID);
                m_mem = new MemCtlr(this);
            }

            m_inj_pool = Simulator.controller.newPrioPktPool(m_coord.ID);
            Simulator.controller.setInjPool(m_coord.ID, m_inj_pool);
            m_injQueue_flit = new Queue<Flit>();
            m_injQueue_evict = new Queue<Flit>();
			m_injQueue_multi_flit = new Queue<Flit> [Config.sub_net];
			for (int i=0; i<Config.sub_net; i++)
				m_injQueue_multi_flit[i] = new Queue<Flit> ();
            m_local = new Queue<Packet>();

            m_rxbuf_naive = new RxBufNaive(this,
                    delegate(Flit f) { m_injQueue_evict.Enqueue(f); },
                    delegate(Packet p) { receivePacket(p); });
        }

        public void setRouter(Router r)
        {
            m_router = r;
        }


		
		void synthGen(double rate)
		{
			if (Simulator.rand.NextDouble() < rate)
			{
				if (m_inj_pool.Count > Config.synthQueueLimit)
					Simulator.stats.synth_queue_limit_drop.Add();
				else
				{
					int dest = m_coord.ID;
					Coord c = new Coord(dest);
					switch (Config.synthPattern) {
						case SynthTrafficPattern.UR:
						dest = Simulator.rand.Next(Config.N);
						c = new Coord(dest);
						break;
						case SynthTrafficPattern.BC:
						dest = ~dest & (Config.N - 1);
						c = new Coord(dest);
						break;
						case SynthTrafficPattern.TR:
						c = new Coord(m_coord.y, m_coord.x);
						break;
					}
					int packet_size;
					if (Config.uniform_size_enable == true) 
						if (Config.topology == Topology.Mesh_Multi)
							packet_size = Config.uniform_size * Config.sub_net;
						else
							packet_size = Config.uniform_size;
					else
					{
						if (Simulator.rand.NextDouble() < 0.5) packet_size = Config.router.addrPacketSize;
						else  packet_size = Config.router.dataPacketSize;
					}
					Packet p = new Packet(null,0,packet_size,m_coord, c);
					queuePacket(p);
				}
			}
		}

        public void doStep()
        {

			if (Config.synthGen)
			{
				synthGen(Config.synth_rate);
			}

            while (m_local.Count > 0 &&
                    m_local.Peek().creationTime < Simulator.CurrentRound)
            {
                receivePacket(m_local.Dequeue());
            }

            if (m_cpu != null)
                m_cpu.doStep();
            if (m_mem != null)
                m_mem.doStep();

            if (m_inj_pool.FlitInterface) // By Xiyue: ??? why 2 different injection modes?
            {
                Flit f = m_inj_pool.peekFlit();
                if (f != null && m_router.canInjectFlit(f))
                {
                    m_router.InjectFlit(f);  
                    m_inj_pool.takeFlit();  // By Xiyue: ??? No action ???
                }
            }
			else // By Xiyue: Actual injection into network
            {
                Packet p = m_inj_pool.next();

				if (Config.topology == Topology.Mesh_Multi) {
					// serialize packet to flit and select a subnetwork
					// assume infinite NI buffer
					int selected = -1;
					selected = Simulator.rand.Next(Config.sub_net);

					if (p != null && p.creationTime <= Simulator.CurrentRound)
						foreach (Flit f in p.flits)
							m_injQueue_multi_flit[selected].Enqueue(f);

					//int load = Simulator.stats.inject_flit.Count / Config.N / Simulator.CurrentRound / Config.sub_net;
					/*
					int inject_trial = 0;
					int [] inject_trial_subnet;
					inject_trial_subnet = new int[Config.sub_net];
					for (int i = 0 ; i < Config.sub_net; i++)
						inject_trial_subnet [i] = 0;
					*/

					for (int i = 0 ; i < Config.sub_net; i++)
					{
						if (m_injQueue_multi_flit[i].Count > 0 && m_router.canInjectFlitMultNet(i, m_injQueue_multi_flit[i].Peek()))
						{
							Flit f = m_injQueue_multi_flit[i].Dequeue();
							m_router.InjectFlitMultNet(i, f);
							//inject_trial_subnet [i] ++;
						}
						/*
						else if (m_injQueue_multi_flit[i].Count == 0)
						{
							// randomly pick a non-empty inject queue, cannot be itself, and each subnet cannot inject more than twice
							selected = Simulator.rand.Next(Config.sub_net);
							int find_trial = 0; // only loop 8 times to ensure not stuck there forever.
							while ((selected == i || m_injQueue_multi_flit[selected].Count == 0 || inject_trial_subnet [selected] >= 2) && (find_trial < 8))
							{
								find_trial ++;
								selected = Simulator.rand.Next(Config.sub_net);
							}
							if (m_injQueue_multi_flit[selected].Count > 0 && m_router.canInjectFlitMultNet(i, m_injQueue_multi_flit[selected].Peek()))
							{
								Flit f = m_injQueue_multi_flit[selected].Dequeue();
								m_router.InjectFlitMultNet(i, f);
								inject_trial_subnet [selected] ++;
							}

						}*/
					}
				}
				else
				{

	                if (p != null)
	                {
	                    foreach (Flit f in p.flits)
	                        m_injQueue_flit.Enqueue(f);
	                }

					if (m_injQueue_evict.Count > 0 && m_router.canInjectFlit(m_injQueue_evict.Peek())) // By Xiyue: ??? What is m_injQueue_evict ?
	                {
	                    Flit f = m_injQueue_evict.Dequeue();
	                    m_router.InjectFlit(f);
	                }
					else if (m_injQueue_flit.Count > 0 && m_router.canInjectFlit(m_injQueue_flit.Peek())) // By Xiyue: ??? Dif from m_injQueue_evict?
	                {
	                    Flit f = m_injQueue_flit.Dequeue();
	#if PACKETDUMP
	                    if (f.flitNr == 0)
	                        if (m_coord.ID == 0)
	                            Console.WriteLine("start to inject packet {0} at node {1} (cyc {2})",
	                                f.packet, coord, Simulator.CurrentRound);
	#endif

	                    m_router.InjectFlit(f);  // by Xiyue: inject into a router

	                    // for Ring based Network, inject two flits if possible
	                    for (int i = 0 ; i < Config.RingInjectTrial - 1; i++)
							if (m_injQueue_flit.Count > 0 && m_router.canInjectFlit(m_injQueue_flit.Peek()))
	    	                {
	        	            	f = m_injQueue_flit.Dequeue();
	            	        	m_router.InjectFlit(f);
	                	    }
	                }
				}
            }
        }

        public virtual void receiveFlit(Flit f)
        {
            if (Config.naive_rx_buf)
                m_rxbuf_naive.acceptFlit(f);
            else
               receiveFlit_noBuf(f);
       }

        void receiveFlit_noBuf(Flit f)
        {
            f.packet.nrOfArrivedFlits++;
            if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
                receivePacket(f.packet);
        }

        public void evictFlit(Flit f)
        {
            m_injQueue_evict.Enqueue(f);
        }

        public void receivePacket(Packet p)
        {
#if PACKETDUMP
            if (m_coord.ID == 0)
            Console.WriteLine("receive packet {0} at node {1} (cyc {2}) (age {3})",
                p, coord, Simulator.CurrentRound, Simulator.CurrentRound - p.creationTime);
#endif

            if (p is RetxPacket)
            {
                p.retx_count++;
                p.flow_open = false;
                p.flow_close = false;
                queuePacket( ((RetxPacket)p).pkt );
            }
            else if (p is CachePacket)
            {
                CachePacket cp = p as CachePacket;
                m_cpu.receivePacket(cp); // by Xiyue: Local ejection
            }
        }
        
		public void queuePacket(Packet p) // By Xiyue: called by CmpCache::send_noc() 
        {
#if PACKETDUMP
            if (m_coord.ID == 0)
            Console.WriteLine("queuePacket {0} at node {1} (cyc {2}) (queue len {3})",
                    p, coord, Simulator.CurrentRound, queueLens);
#endif
            if (p.dest.ID == m_coord.ID) // local traffic: do not go to net (will confuse router) // by Xiyue: just hijack the packet if it only access the shared cache at the local node.
            {
                m_local.Enqueue(p);  // this is filter out
                return;
            }

            if (Config.idealnet) // ideal network: deliver immediately to dest
                Simulator.network.nodes[p.dest.ID].m_local.Enqueue(p);
            else // otherwise: enqueue on injection queue
            {
                m_inj_pool.addPacket(p); //By Xiyue: Actual Injection. But core execution is still a question.
            }
        }

        public void sendRetx(Packet p)
        {
            p.nrOfArrivedFlits = 0;
            RetxPacket pkt = new RetxPacket(p.dest, p.src, p);
            queuePacket(pkt);
        }

        public bool Finished
        { get { return (m_cpu != null) ? m_cpu.Finished : false; } }

        public bool Livelocked
        { get { return (m_cpu != null) ? m_cpu.Livelocked : false; } }

        public void visitFlits(Flit.Visitor fv)
        {
            // visit in-buffer (inj and reassembly) flits too?
        }
    }
}
