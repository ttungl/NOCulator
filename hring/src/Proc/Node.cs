//#define PACKETDUMP
//#define PROMISEDUMP

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

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

	public class Interference
	{
		private CmpCache_Txn _txn;
		private Request _request;
		private int _requesterID;
		private bool valid;
		private int interference_cycle;
		private int _mshr;

		public Interference (Packet pkt)
		{
			_txn = pkt.txn;
			_mshr = pkt.txn.mshr;
			_request = pkt.request;
			_requesterID = pkt.requesterID;
			valid = true;
			interference_cycle = pkt.intfCycle;
		}

		public Interference ()
		{
			_txn = null;
			_request = null;
			_requesterID = 0;
			valid = false;
			interference_cycle = 0;
		}

		public bool compare (Packet pkt) 
		{
			if (pkt.txn.mshr == _mshr && pkt.requesterID == _requesterID)
				return true;
			else
				return false;
		}

		public int intfCycle 
		{
			get {return interference_cycle;}
		}
	}


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
		//private ArrayList m_inheritance_table;
		private Dictionary <string, int> m_inheritance_dict;

        private Router m_router;

        private IPrioPktPool m_inj_pool;
        private Queue<Flit> m_injQueue_flit, m_injQueue_evict;
        public Queue<Packet> m_local;

        public int RequestQueueLen { get { return m_inj_pool.FlitCount + m_injQueue_flit.Count; } }

        RxBufNaive m_rxbuf_naive;


		public int get_txn_intf (Packet new_inj_pkt) {
			int intfCycle = 0;
			string key = new_inj_pkt.requesterID.ToString() + (new_inj_pkt.txn.mshr + Config.N).ToString();
			if (m_inheritance_dict.ContainsKey (key)) {
				intfCycle = m_inheritance_dict [key];
				m_inheritance_dict.Remove (key);
			}
			/*
			foreach (Interference i in m_inheritance_table)
			{
				if (i.compare(new_inj_pkt))
				{
					intfCycle = i.intfCycle;
					m_inheritance_table.Remove(i);
					return intfCycle; 
				}
			}
			*/
			return intfCycle;
		}

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
            m_local = new Queue<Packet>();
			//m_inheritance_table =  new ArrayList();
			m_inheritance_dict = new Dictionary<string, int> ();

            m_rxbuf_naive = new RxBufNaive(this,
                    delegate(Flit f) { m_injQueue_evict.Enqueue(f); },
                    delegate(Packet p) { receivePacket(p); });



        }

        public void setRouter(Router r)
        {
            m_router = r;
        }

        public void doStep()
        {
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

                if (p != null)
                {
					int intfCycle = get_txn_intf(p);
                    foreach (Flit f in p.flits)
					{
						f.intfCycle = intfCycle;
                        m_injQueue_flit.Enqueue(f);
					}
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

        public virtual void receiveFlit(Flit f)
        {
            if (Config.naive_rx_buf)
                m_rxbuf_naive.acceptFlit(f);
            else
               receiveFlit_noBuf(f);
       }

        void receiveFlit_noBuf(Flit f)
        {
			// Register the flit interferece delay
			// intfCycle gets updates only at the first and last flit of a packet arrival.
			f.packet.nrOfArrivedFlits++;
			if (f.packet.nrOfArrivedFlits == 1)
			{
				// Record the inteference cycle of flits.
				f.packet.intfCycle = f.intfCycle;  // the interference cycle of head flit
				f.packet.first_flit_arrival = Simulator.CurrentRound;
			}

			if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
			{
				// Compute the inteference cycle of a pacekt.
				f.packet.intfCycle = (int)f.packet.intfCycle + ((int)Simulator.CurrentRound - (int)f.packet.first_flit_arrival - f.packet.nrOfFlits + 1); // assume the flits of will arrive consecutively without interference. In case of control packet, the portion inside of parenthesis is 0.
				if (f.packet.intfCycle  > 0 && f.packet.requesterID != m_cpu.ID && f.packet.critical == true)
				{
					// only log the delay of the packets whose associated request is not generated by the current node
					// therefore, they will trigger some packets later.
					string inheritance_key = f.packet.requesterID.ToString() + (f.packet.txn.mshr + Config.N).ToString();
					m_inheritance_dict.Add(inheritance_key, f.packet.intfCycle);
					// m_inheritance_table.Add(intf_entry);
					// profile size of the m_inheritance_table
					Simulator.stats.inherit_table_size.Add(m_inheritance_dict.Count);
				}
				receivePacket(f.packet);
			}
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
                m_inj_pool.addPacket(p); //By Xiyue: Actual Injection.
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
