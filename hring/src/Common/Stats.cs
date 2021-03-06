using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Globalization;

namespace ICSimulator
{
    public class Stats
    {
		// By Xiyue: for coherent packet profiling
		//public SampledStat[] avg_slowdown_error;
		public SampledStat flit_intf, packet_intf, request_intf, app_stall_per_epoch;
		public AccumStat false_block, correct_block;
		public AccumStat priority_inv;
		public PeriodicAccumStat[] estimated_slowdown;
		public PeriodicAccumStat[] L1miss_persrc_period;
		//public PeriodicAccumStat[] estimated_slowdown_period;
		public PeriodicAccumStat[] noc_stc; // noc-level stall time criticality
		public PeriodicAccumStat[] app_rank;
		public AccumStat [] throttle_down_profile; 
		public AccumStat [] cpu_stall_throttle;
		public AccumStat opt_fair, opt_perf;
		public SampledStat[] mshrs_credit, mshrs_util;
		public SampledStat inherit_table_size;
		public SampledStat netutil;

		// AFC -- buffer power
		public AccumStat afc_buf_enabled, afc_buf_write, afc_buf_read, afc_xbar;
		public AccumStat[] afc_buf_enabled_bysrc, afc_buf_write_bysrc, afc_buf_read_bysrc, afc_xbar_bysrc;
		// AFC -- switching stats
		public AccumStat afc_switch, afc_switch_bless, afc_switch_buf;
		public AccumStat[] afc_switch_bysrc, afc_switch_bless_bysrc, afc_switch_buf_bysrc;
		public AccumStat afc_buffered, afc_bless, afc_gossip;
		public AccumStat[] afc_buffered_bysrc, afc_bless_bysrc, afc_gossip_bysrc;
		public SampledStat afc_avg;
		public SampledStat[] afc_avg_bysrc;
		public SampledStat afc_buf_occupancy;
		public SampledStat[] afc_buf_occupancy_bysrc;

		public AccumStat[] afc_vnet;
		public AccumStat afc_bufferBypass;

		public SampledStat stretch;
		public SampledStat[] stretch_bysrc, stretch_bydest;
		//public SampledStat[,] stretch_srcdest;
		public SampledStat minpath;
		public SampledStat[] minpath_bysrc;
		//public SampledStat [] netslow_bysrc;

		//public SampledStat [] fairness_ie, fairness_ic;
		//public SampledStat [] fairness_slowdown, fairness_texcess;
		//public SampledStat [] fairness_ie_perpkt, fairness_ic_perpkt;

		//public SampledStat [] injqueue_bysrc;
		//public SampledStat injqueue;

		public SampledStat[] fairness_ie_starve_perpkt, fairness_ie_defl_perpkt;

		// ---- SCARAB impl
		public AccumStat drop;
		public AccumStat[] drop_by_src;
		public AccumStat nack_unavail;
		public AccumStat[] nack_unavail_by_src;

		private static List<StatsObject> m_subobjects = new List<StatsObject>();

		//public PeriodicAccumStat[] actual_slowdown;
		public AccumStat[] active_cycles;
		public AccumStat[] non_overlap_penalty;
		public SampledStat[] queue_delay;
		public SampledStat[] serialization_latency;
		public CacheProfileStat[] pkt_tier_count;
		public PeriodicAccumStat[] non_overlap_penalty_period;
		public PeriodicAccumStat[] causeIntf;
		public PeriodicAccumStat[] insns_persrc_ewma;
		public PeriodicAccumStat[] insns_persrc_period;
		public SampledStat net_latency, total_latency;
		public SampledStat[] net_latency_bysrc, total_latency_bysrc;
		public SampledStat[] net_latency_bydest, total_latency_bydest;
		public SampledStat[] flit_inj_latency_byapp, flit_net_latency_byapp, flit_total_latency_byapp;
		public SampledStat flit_inj_latency, flit_net_latency, flit_total_latency;

		public class LogStat : StatsObject
		{
  
			private List<double> history = new List<double>();
			
	        public override void Reset()
	        {
	            history.Clear();
	        }
	
			public void Add(double val)
	        {
	            history.Add(val);
	        }

			public double Peek (int i)
			{
				return history[i];
			}
	
	        public override void DumpJSON(TextWriter tw)
	        {
				tw.Write (Environment.NewLine);
	            tw.Write("{");
				foreach (double i in history)
					tw.Write ("{0},",Math.Round(i,3));
				tw.Write("}");
	        }
    
		} 
		
		LogStat newLogStat()
		{
			LogStat ret = new LogStat();
			return ret;
		}

		// an AccumStat is a statistic that counts discrete events (flit
		// deflections, ...) and from which we can extract a rate
		// (events/time).
		public class CacheProfileStat : StatsObject
		{
			protected double m_count;

			public CacheProfileStat ()
			{
				Reset();
			}

			public void Add()
			{
				m_count++;
			}

			public void Add(ulong addee)
			{
				m_count += (ulong) addee;
			}

			public void Add(double addee)
			{
				m_count += addee;
			}
				
			public override void Reset()
			{
				m_count = 0;
			}


			public override void DumpJSON(TextWriter tw)
			{
				tw.Write("{0}", m_count);
			}

			public double Count
			{ get { return m_count; } }

		}


		CacheProfileStat[] newCacheProfileStatArray()
		{
			CacheProfileStat[] ret = new CacheProfileStat[30];
			for (int i = 0; i < 30; i++)
				ret[i] = new CacheProfileStat(); // initialize private variable
			return ret;
		}

		// end Xiyue


        public int N;
        ulong m_finishtime;

        public AccumStat cycle;
        //micro13 rebuttal stat
        public AccumStat bridge0Count;
        public AccumStat bridge1Count;
        public AccumStat bridge2Count;
        public AccumStat bridge3Count;
        public AccumStat bridge4Count;
        public AccumStat bridge5Count;
        public AccumStat bridge6Count;
        public AccumStat bridge7Count;
        //processor stats
        //public AccumStat[] active_cycles;
        public AccumStat[] idle_cycles;
        public AccumStat[] active_cycles_alone;
        public ConstAccumStat[] skipped_insns_persrc;
        public ConstAccumStat[] warming_insns_persrc;
        public AccumStat[] insns_persrc;
        public AccumStat[] every_insns_persrc;

        public AccumStat[] opp_buff_preventable_stalls_persrc;
        //public EnumStat<StallSources>[] front_stalls_persrc;
        //public EnumStat<StallSources>[] back_stalls_persrc;
        //public EnumStat<StallSources>[] mem_back_stalls_persrc;
        //public EnumStat<StallSources>[] nonmem_back_stalls_persrc;
        public enum StallSources
        {
            MEMORY,
            LACK_OF_MSHRS,
            NOTHING_TO_RETIRE,
            ADDR_PACKET,
            DATA_PACKET,
            MC_ADDR_PACKET,
            MC_DATA_PACKET,
            INJ_ADDR_PACKET,
            INJ_DATA_PACKET,
            INJ_MC_ADDR_PACKET,
            INJ_MC_DATA_PACKET
        };
        //network stalls: packetOffset + (2*(int)p.packetType) + 1 if in injectionQueue
        public AccumStat[] cold_accesses_persrc;
        public AccumStat[] L1_accesses_persrc;
        public AccumStat[] L1_hits_persrc;
        public AccumStat[] L1_misses_persrc;
        public AccumStat[] L1_upgr_persrc;
        public AccumStat[] L1_c2c_persrc;
        public AccumStat[] L1_evicts_persrc;
        public AccumStat[] L1_writebacks_persrc;
        public AccumStat[] L2_accesses_persrc;
        public AccumStat[] L2_hits_persrc;
        public AccumStat[] L2_misses_persrc;
        public AccumStat[] L2_evicts_persrc;
        public AccumStat[] L2_writebacks_persrc;

        public AccumStat l1_warmblocks, l1_totblocks; // L1 warming stats
        public AccumStat l2_warmblocks, l2_totblocks; // L2 warming stats

        public SampledStat deadline;
        public SampledStat req_rtt;

        // network-level stats
        public AccumStat inject_flit, eject_flit, inject_flit_head;
        public AccumStat[] inject_flit_bysrc, eject_flit_bydest;

        // throttling stats
        public AccumStat[] throttled_counts_persrc;
        public AccumStat[] not_throttled_counts_persrc;
        public SampledStat total_th_off;
        public SampledStat[] mpki_bysrc;
        public SampledStat allowed_sum_mpki;
        public SampledStat total_sum_mpki;
        public SampledStat total_th_rate;
        //ipc difference b/w free-injecting and throttled interval for each app
        //In the uniform controller, each app is put into a cluster, so that
        //the interference is minimized.
        public SampledStat[] ipc_diff_bysrc;
        public AccumStat[] low_cluster;
        public AccumStat[] rr_cluster;
        public AccumStat[] high_cluster;

        //public AccumStat[,] inject_flit_srcdest;
        public AccumStat[] inject_flit_req;

        public SampledStat flit_traversals, deflect_flit, unprod_flit;
        public SampledStat[] deflect_flit_byinc, unprod_flit_byinc;
        public SampledStat[] deflect_perflit_byreq;

        public AccumStat[] deflect_flit_bysrc, deflect_flit_byloc,deflect_flit_byreq;
        public AccumStat[] unprod_flit_bysrc, unprod_flit_byloc;

        public AccumStat starve_flit;
		public AccumStat[] starve_flit_bysrc;          
        public SampledStat[] starve_interval_bysrc;

        public SampledStat net_decisionLevel;
        //public AccumStat [] intdefl_bysrc;

        //public SampledStat send_buf, rcv_buf;
        //public SampledStat [] send_buf_bysrc, rcv_buf_bydest;


        //public SampledStat[,] net_latency_srcdest, total_latency_srcdest;

        //In HighOther state
        public AccumStat[] throttle_time_bysrc;
        //In AlwaysThrottled state
        public AccumStat[] always_throttle_time_bysrc;

		public SampledStat hoq_latency;  
        public SampledStat[] hoq_latency_bysrc;

        public SampledStat golden_pernode; // golden flits per node, per cycle (0, 1, 2, 3, 4)
        public AccumStat[] golden_bycount; // histogram of the above

        public AccumStat[] traversals_pernode;
        //public AccumStat[,] traversals_pernode_bysrc; 

        public AccumStat flow_open, flow_close, flow_retx;

        //public SampledStat [] flit_head_latency_byapp;
        //public SampledStat flit_head_latency;

        

        // energy stats
        // TODO

        public SampledStat[] tier1_unstarve;

        public DictSampledStat[] compute_episode_persrc;
        public DictSampledStat[] network_episode_persrc;
        public DictSampledStat[] memory_episode_persrc;
        public DictSampledStat[] nonmemory_episode_persrc;

        // Memory Stats
        //public AccumStat[,] bank_access_persrc;
        //public AccumStat[,] bank_rowhits_persrc;
        //public SampledStat[,] bank_queuedepth_persrc;
        public SampledStat[] bank_queuedepth;

        // ---- Live/Deadlock paper
        public AccumStat[] reflect_flit_bysrc;
        public AccumStat reflect_flit;
        public SampledStat[] buf_usage_bysrc;

        //Router prioritization counters
        public PeriodicAccumStat[] L1_misses_persrc_period;
		public PeriodicAccumStat[] inAccumStatsns_persrc_period;
        public PeriodicAccumStat[] outstandingReq_persrc_period;
        public PeriodicAccumStat[] weightedOutstandingReq_persrc_period;
        public PeriodicAccumStat[] cycles_persrc_period;

        public AccumStat[] cpu_sync_memdep, cpu_sync_lock, cpu_sync_barrier;
        public AccumStat[] cpu_stall, cpu_stall_mem;
        public AccumStat[] promise_wait;
        public SampledStat[] promises_local, promises_local_queued;
        //public SampledStat[,] promises_remote_wait, promises_remote_surplus;

        public AccumStat livelock; // one event counted if we stop due to livelock

        public AccumStat retx_once; // Retransmit-Once retx
        public SampledStat retx_once_slots;

        // slack stats
        public SampledStat[] all_slack_persrc;
        public SampledStat[] net_slack_persrc;
        public SampledStat[] mem_slack_persrc;
        public SampledStat all_slack;
        public SampledStat net_slack;
        public SampledStat mem_slack;

        // stall stats
        public SampledStat[] all_stall_persrc;
        public SampledStat[] net_stall_persrc;
        public SampledStat[] mem_stall_persrc;
        public SampledStat all_stall;
        public SampledStat net_stall;
        public SampledStat mem_stall;

        public SampledStat[] deflect_perdist;

        public SampledStat[] mshrs_persrc;
        public SampledStat mshrs;
        
        //RingCLustered 
       /* public AccumStat[,] nodeRouterAccess;
        public AccumStat[,] connectRouterAccess;
        public AccumStat[,] connectRouter_deflected;
        public AccumStat[,] connectRouter_traversed;
        public AccumStat[,] connectRouter_nonproductive;
        public SampledStat[,] bufferDepth;*/
		public AccumStat bypassConnect;
		public AccumStat crossXConnect;
		public AccumStat crossYConnect;

    	public SampledStat timeInTheSourceRing;
		public SampledStat timeInTheTransitionRing;
		public SampledStat timeInTheDestRing;
		public SampledStat timeWaitToInject;
		public SampledStat timeInBuffer1hop;
		public SampledStat timeInBuffer2hop;
		public SampledStat netLatency_local;
		public SampledStat netLatency_1hop;
		public SampledStat netLatency_2hop;
		public AccumStat flitLocal;
		public AccumStat flitL1Global;
		public AccumStat flit1hop;
		public AccumStat flit2hop;
		public SampledStat local_net_util;
		public SampledStat global_net_util;	
		public SampledStat g1_net_util;
		public SampledStat g2_net_util;
		public SampledStat timeInGR1hop;
		public SampledStat timeInGR2hop;

		public AccumStat[] flitsTryToEject;
		public SampledStat ejectsFromSamePacket;
		public SampledStat ejectTrial;
		public SampledStat minNetLatency;
		public SampledStat destDeflectedNetLatency;
		public SampledStat destDeflectedMinLatency;
		public AccumStat multiEjectTrialFlits;
		public AccumStat singleEjectTrialFlits;
		public AccumStat starveTriggered; 
		public AccumStat observerTriggered;

		public AccumStat allNodeThrottled;
		public AccumStat injStarvation;

		public AccumStat flitsToRouter;
		public AccumStat flitsToHRnode;
		public AccumStat flitsToHRbridge;
		public AccumStat flitsPassBy;
		public AccumStat LdeflectedFlit;
		public AccumStat GdeflectedFlit;
		public AccumStat G2LcrossFlit;
		public AccumStat L2GcrossFlit;
		public AccumStat G2LBufferBypass;
		public AccumStat L2GBufferBypass;
		
        // Buffered Ring stats
		public AccumStat totalBufferEnqCount;

		public SampledStat avgBufferDepth;


        public AccumStat bufrings_nic_enqueue, bufrings_nic_dequeue;
        public AccumStat bufrings_nic_inject, bufrings_nic_eject;
        public AccumStat bufrings_nic_starve;

        public AccumStat[] bufrings_iri_enqueue_g, bufrings_iri_dequeue_g;
        public AccumStat[] bufrings_iri_enqueue_l, bufrings_iri_dequeue_l;
        public AccumStat[] bufrings_iri_enqueue_gl, bufrings_iri_dequeue_gl;
        public AccumStat[] bufrings_iri_enqueue_lg, bufrings_iri_dequeue_lg;
        public AccumStat[] bufrings_link_traverse;

        public SampledStat[] bufrings_iri_occupancy_g;
        public SampledStat[] bufrings_iri_occupancy_l;
        public SampledStat[] bufrings_iri_occupancy_gl;
        public SampledStat[] bufrings_iri_occupancy_lg;
        public SampledStat bufrings_nic_occupancy;

        public SampledStat[] bufrings_ring_util;

        public AccumStat bufrings_nuke;

        AccumStat newAccumStat()
        {
            AccumStat ret = new AccumStat();
            return ret;
        }

        AccumStat[] newAccumStatArray()
        {
            AccumStat[] ret = new AccumStat[N];
            for (int i = 0; i < N; i++)
                ret[i] = new AccumStat();
            return ret;
        }

        ConstAccumStat[] newConstAccumStatArray()
        {
            ConstAccumStat[] ret = new ConstAccumStat[N];
            for (int i = 0; i < N; i++)
                ret[i] = new ConstAccumStat();
            return ret;
        }

        AccumStat[,] newAccumStatArray2D()
        {
            AccumStat[,] ret = new AccumStat[N, N];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    ret[i, j] = new AccumStat();
            return ret;
        }

        const int NumBins = 2500; // arbitrary

        SampledStat newSampledStat()
        {
            SampledStat ret = new SampledStat(NumBins, 0, NumBins);
            return ret;
        }

        SampledStat[] newSampledStatArray()
        {
            return newSampledStatArray(false);
        }

        SampledStat[] newSampledStatArray(bool bins)
        {
            SampledStat[] ret = new SampledStat[N];
            for (int i = 0; i < N; i++)
            {
                if (!bins)
                    ret[i] = new SampledStat();
                else
                    ret[i] = new SampledStat(NumBins, 0, NumBins);
            }
            return ret;
        }

        DictSampledStat[] newDictSampledStatArray()
        {
            DictSampledStat[] ret = new DictSampledStat[N];
            for (int i = 0; i < N; i++)
                ret[i] = new DictSampledStat();
            return ret;
        }

        /*
        EnumStat<StallSources>[] newEnumSampledStatArray(int binSize)
        {
            EnumStat<StallSources>[] ret = new EnumStat<StallSources>[N];
            for (int i = 0; i < N; i++)
            {
                ret[i] = new EnumStat<StallSources>();
            }
            return ret;
        }
        */

        SampledStat[,] newSampledStatArray2D()
        {
            SampledStat[,] ret = new SampledStat[N, N];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    ret[i, j] = new SampledStat(); // no bins for arrays
                }
            return ret;
        }

        PeriodicAccumStat[] newPeriodicAccumStatArray()
        {
            PeriodicAccumStat[] ret = new PeriodicAccumStat[N];
            for (int i = 0; i < N; i++)
                ret[i] = new PeriodicAccumStat();
            return ret;
        }

        public Stats(int nrNodes)
        {
            Init(nrNodes);
        }

        public void Init(int nrNodes)
        {
            N = nrNodes;
            m_subobjects = new List<StatsObject>();

            //First, do the non standard constructors
            //front_stalls_persrc = newEnumSampledStatArray(Enum.GetValues(typeof(StallSources)).Length);
            //back_stalls_persrc = newEnumSampledStatArray(Enum.GetValues(typeof(StallSources)).Length);
            //mem_back_stalls_persrc = newEnumSampledStatArray(Enum.GetValues(typeof(StallSources)).Length);
            //nonmem_back_stalls_persrc = newEnumSampledStatArray(Enum.GetValues(typeof(StallSources)).Length);

            //bank_access_persrc = new AccumStat[Config.memory.bank_max_per_mem * Config.memory.mem_max, N];
            //bank_rowhits_persrc = new AccumStat[Config.memory.bank_max_per_mem * Config.memory.mem_max, N];
            //Console.WriteLine(Config.memory.bank_max_per_mem.ToString() + '\t' + Config.memory.mem_max.ToString());
            //bank_queuedepth_persrc = new SampledStat[Config.memory.bank_max_per_mem * Config.memory.mem_max, N];
            bank_queuedepth = new SampledStat[Config.memory.bank_max_per_mem * Config.memory.mem_max];
            for (int i = 0; i < Config.memory.bank_max_per_mem * Config.memory.mem_max; i++)
            {
                bank_queuedepth[i] = new SampledStat();
                for (int j = 0; j < N; j++)
                {
                    //bank_access_persrc[i, j] = new AccumStat();
                    //bank_rowhits_persrc[i, j] = new AccumStat();

                    //bank_queuedepth_persrc[i, j] = new SampledStat();
                }
            }

            //Fill each other field with the default constructor
            foreach (FieldInfo fi in GetType().GetFields())
            {
                if (fi.GetValue(this) != null)
                    continue;

                Type t = fi.FieldType;

				if (t == typeof(DictSampledStat[]))
					fi.SetValue (this, newDictSampledStatArray ());
				else if (t == typeof(PeriodicAccumStat[]))
					fi.SetValue (this, newPeriodicAccumStatArray ());
                else if (t == typeof(AccumStat))
                    fi.SetValue(this, newAccumStat());
                else if (t == typeof(AccumStat[]))
                    fi.SetValue(this, newAccumStatArray());
                else if (t == typeof(AccumStat[,]))
                    fi.SetValue(this, newAccumStatArray2D());

                else if (t == typeof(ConstAccumStat[]))
                    fi.SetValue(this, newConstAccumStatArray());

                else if (t == typeof(SampledStat))
                    fi.SetValue(this, newSampledStat());
                else if (t == typeof(SampledStat[]))
                    fi.SetValue(this, newSampledStatArray());
                else if (t == typeof(SampledStat[,]))
                    fi.SetValue(this, newSampledStatArray2D());
				else if (t == typeof(CacheProfileStat[]))
					fi.SetValue(this, newCacheProfileStatArray());
				else if (t == typeof(LogStat))
					fi.SetValue(this, newLogStat());
				
            }
        }

        public void Reset()
        {
            foreach (StatsObject s in m_subobjects)
                s.Reset();
        }

        public void DumpJSON(TextWriter tw)
        {
            tw.WriteLine("{");

            bool first = true;
            foreach (FieldInfo fi in GetType().GetFields())
            {
                object o = fi.GetValue(this);
                if (o is StatsObject || o is object[] || o is object[,])
                {
                    if (!first)
                        tw.WriteLine(",");
                    else
                        first = false;

                    tw.Write("\"{0}\":", fi.Name);
                    DumpJSON(tw, o);
                }
                else
                    Console.WriteLine("not dumping "+fi.Name);
            }
            tw.WriteLine("}");
        }

        public void DumpJSON(TextWriter tw, object o)
        {
            if (o is StatsObject)
                ((StatsObject)o).DumpJSON(tw);
            if (o is ulong)
                tw.Write("{0}", (ulong)o);
            if (o is object[])
            {
                bool first = true;
                tw.Write("[");
                foreach (object elem in (object[])o)
                {
                    if (first) first = false;
                    else tw.Write(",");
                    DumpJSON(tw, elem);
                }
                tw.Write("]");
            }
            if (o is object[,])
            {
                object[,] arr2D = (object[,])o;
                int dim0 = arr2D.GetUpperBound(0) + 1, dim1 = arr2D.GetUpperBound(1) + 1;
                object[][] arr = new object[dim0][];
                for (int i = 0; i < dim0; i++)
                {
                    arr[i] = new object[dim1];
                    for (int j = 0; j < dim1; j++)
                        arr[i][j] = arr2D[i, j];
                }
                DumpJSON(tw, arr);
            }
        }

        public void DumpMATLAB(TextWriter tw)
        {
            tw.WriteLine("dimX = {0}; dimY = {1};", Config.network_nrX, Config.network_nrY);
            tw.WriteLine("cycles = {0};", m_finishtime);

            foreach (FieldInfo fi in GetType().GetFields())
            {
                object o = fi.GetValue(this);
                if (o is AccumStat || o is SampledStat)
                {
                    object[,] arr = new object[1, 1] { { o } };
                    DumpMATLAB(tw, fi.Name, arr);
                }
                if (o is object[])
                {
                    object[] a = (object[])o;
                    object[,] arr = new object[1, a.Length];
                    for (int i = 0; i < a.Length; i++)
                        arr[0, i] = a[i];
                    DumpMATLAB(tw, fi.Name, arr);
                }
                if (o is object[,])
                    DumpMATLAB(tw, fi.Name, (object[,])o);
            }
        }

        void DumpMATLAB(TextWriter tw, double val)
        {
            if (val == Double.PositiveInfinity)
                tw.Write("inf,");
            else if (val == Double.NegativeInfinity)
                tw.Write("-inf,");
            else
                tw.Write("{0},", val);
        }

        void DumpMATLAB(TextWriter tw, string name, object[,] o)
        {
            int dim0 = o.GetUpperBound(0) + 1, dim1 = o.GetUpperBound(1) + 1;

            if (o[0, 0] is AccumStat)
            {
                tw.WriteLine("{0} = [", name);
                for (int x = 0; x < dim0; x++)
                {
					for (int y = 0; y < dim1; y++)
                    {
                        tw.Write("{0},", ((AccumStat)o[x, y]).Count);
                    }
                    tw.WriteLine();
                }
                tw.WriteLine("];");
            }
            else if (o[0, 0] is SampledStat)
            {
                tw.WriteLine("{0}_avg = [", name);
                for (int x = 0; x < dim0; x++)
                {
                    for (int y = 0; y < dim1; y++)
                    {
                        DumpMATLAB(tw, ((SampledStat)o[x, y]).Avg);
                    }
                    tw.WriteLine();
                }
                tw.WriteLine("];");

                tw.WriteLine("{0}_min = [", name);
                for (int x = 0; x < dim0; x++)
                {
                    for (int y = 0; y < dim1; y++)
                    {
                        DumpMATLAB(tw, ((SampledStat)o[x, y]).Min);
                    }
                    tw.WriteLine();
                }
                tw.WriteLine("];");

                tw.WriteLine("{0}_max = [", name);
                for (int x = 0; x < dim0; x++)
                {
                    for (int y = 0; y < dim1; y++)
                    {
                        DumpMATLAB(tw, ((SampledStat)o[x, y]).Max);
                    }
                    tw.WriteLine();
                }
                tw.WriteLine("];");

                tw.WriteLine("{0}_count = [", name);
                for (int x = 0; x < dim0; x++)
                {
                    for (int y = 0; y < dim1; y++)
                    {
                        tw.Write("{0},", ((SampledStat)o[x, y]).Count);
                    }
                    tw.WriteLine();
                }
                tw.WriteLine("];");

                if (dim0 == 1 && dim1 == 1)
                {
                    ulong[] bins = ((SampledStat)o[0, 0]).Hist;
                    tw.WriteLine("{0}_hist = [", name);
                    for (int i = 0; i < bins.Length; i++)
                        tw.Write("{0},", bins[i]);
                    tw.WriteLine("];");
                }
            }
        }

        public void Finish()
        {
            m_finishtime = Simulator.CurrentRound; // -Config.WarmingDuration;
            /*
            foreach (AccumStat st in m_accum)
                st.Finish(m_finishtime);*/
            foreach (StatsObject so in m_subobjects)
                if (so is AccumStat)
                    ((AccumStat)so).Finish(m_finishtime);
        }

        public void Report(TextWriter tw)
        {
            tw.WriteLine();

            tw.WriteLine("--- Overall");
            tw.WriteLine("      cycles: {0}", m_finishtime);
            tw.WriteLine("      injections: {0} (rate {1:0.0000})", inject_flit.Count, inject_flit.Rate);
            tw.WriteLine("      head flits: {0} (fraction {1:0.0000} of total)", inject_flit_head.Count,
                         (double)inject_flit_head.Count / inject_flit.Count);
            //tw.WriteLine("      deflections: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
            //             deflect_flit.Count, deflect_flit.Rate, deflect_flit.Rate / inject_flit.Rate);
            tw.WriteLine("      starvations: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
                         starve_flit.Count, starve_flit.Rate, starve_flit.Rate / inject_flit.Rate);

            tw.WriteLine("      net latency: {0}", net_latency);
            tw.WriteLine("      tot latency: {0}", total_latency);
            tw.WriteLine("      stretch: {0}", stretch);

            //            tw.WriteLine("      interference: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
            //                         interference.Count, interference.Rate, interference.Rate / inject_flit.Rate);

            for (int i = 0; i < N; i++)
            {
                Coord c = new Coord(i);
				int x = c.x, y = c.y;
                tw.WriteLine("--- Application at ({0},{1}) (config: {2})", x, y,
                             "NOTIMPLEMENTED");
                //String.Join(" ", Simulator.sources.spec.GetSpec(x, y).ToArray()));

                tw.WriteLine("      injections: {0} (rate {1:0.0000})", inject_flit_bysrc[i].Count,
                             inject_flit_bysrc[i].Rate);
                tw.WriteLine("      deflections by source: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
                             deflect_flit_bysrc[i].Count, deflect_flit_bysrc[i].Rate,
                             deflect_flit_bysrc[i].Rate / inject_flit_bysrc[i].Rate);
                tw.WriteLine("      deflections by requester: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
                             deflect_flit_byreq[i].Count, deflect_flit_byreq[i].Rate,
                             deflect_flit_byreq[i].Rate / inject_flit_bysrc[i].Rate);
                tw.WriteLine("      starvations: {0} (rate {1:0.0000} per cycle, {2:0.0000} per flit",
                             starve_flit_bysrc[i].Count, starve_flit_bysrc[i].Rate,
                             starve_flit_bysrc[i].Rate / inject_flit_bysrc[i].Rate);

                tw.WriteLine("      net latency: {0}", net_latency_bysrc[i]);
                tw.WriteLine("      tot latency: {0}", total_latency_bysrc[i]);
                tw.WriteLine("      stretch: {0}", stretch_bysrc[i]);

                //                tw.WriteLine("      ic: {0}", ic_bysrc[i]);
                //                tw.WriteLine("      ie: {0}", ie_bysrc[i]);
                tw.WriteLine();

                /*foreach (Node n in Simulator.network.nodes)
                    n.cpu.output(tw);*/
                    
            }
        }
        public static void addStat(StatsObject so)
        {
            m_subobjects.Add(so);
        }
    }

    public class StatsObject
    {
        public StatsObject()
        {
            Stats.addStat(this);
        }
        public virtual void Reset()
        {
            throw new Exception();
        }
        public virtual void DumpJSON(TextWriter tw)
        {
            throw new Exception();
        }
    }

    // a SampledStat consists of samples of some value (latency,
    // packets in network, ...) from which we can extract a
    // distribution, an average and standard deviation, a min and max,
    // etc.
    public class DictSampledStat : SampledStat
    {
        Dictionary<double, ulong> d;
        public DictSampledStat()
        {
            d = new Dictionary<double, ulong>();
        }

        public override void Add(double val)
        {
            base.Add(val);
            if (d.ContainsKey(val))
                d[val]++;
			else
                d[val] = 1;
        }

        public override void Reset()
        {
            base.Reset();
            if (d != null)
                d.Clear();
        }

        public override void DumpJSON(TextWriter tw)
        {
            tw.Write("{{\"avg\":{0},\"min\":{1},\"max\":{2},\"count\":{3},\"total\":{4},\"bins\":",
                     Avg, Min, Max, Count, Total);
            tw.Write("{");

            bool first = true;
            foreach (KeyValuePair<double, ulong> pair in d)
            {
                if (first) first = false;
                else tw.Write(",");
                tw.Write("\"{0}\":{1}", pair.Key, pair.Value);
            }

            tw.Write("}}");
        }
    }

    public class EnumStat<T> : StatsObject where T : IConvertible
    {
        private double[] m_bins;
        public EnumStat()
        {
            Reset();
        }
        public override void Reset()
        {
            m_bins = new double[Enum.GetValues(typeof(T)).Length];
        }
        public override void DumpJSON(TextWriter tw)
        {
            tw.Write("{");
            bool first = true;
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                tw.Write("{0}\"{1}\":{2}", (first ? "" : ","), Enum.GetName(typeof(T), value), m_bins[value.ToInt32(NumberFormatInfo.InvariantInfo)]);
                first = false;
            }
            tw.Write("}");
        }
        public void Add(int index, double d) //TODO: can we pass value of type enum T?
        {
            m_bins[index] += d;
        }
        public void Add(double[] d)
        {
            for (int i = 0; i < m_bins.Length; i++)
                m_bins[i] += d[i];
        }
    }

    public class SampledStat : StatsObject
    {
        private double m_total, m_sqtotal, m_min, m_max;
        private ulong m_count;

        private ulong[] m_bins;
        private int m_bincount;
        private double m_binmin, m_binmax;

        public SampledStat(int bins, double binmin, double binmax)
        {
            m_bincount = bins;
            m_binmin = binmin;
            m_binmax = binmax;
            Reset();
        }

        public SampledStat()
        {
            m_bincount = 0;
            m_binmin = 0;
            m_binmax = 0;
            Reset();
        }

        public virtual void Add(double val)
        {
            m_total += val;
            m_sqtotal += val * val;
            m_count++;

            if (val < m_min) m_min = val;
            if (val > m_max) m_max = val;

            if (m_bincount > 0)
            {
                int bin = (int)Math.Round((val - m_binmin) /
                                          (m_binmax - m_binmin) * m_bincount);
                if (bin > m_bincount) bin = m_bincount;
                if (bin < 0) bin = 0;
                m_bins[bin]++;
            }
        }

        public override void Reset()
        {
            m_count = 0;
            m_total = 0;
            m_sqtotal = 0;
            m_min = Double.MaxValue;
            m_max = Double.MinValue;

            m_bins = new ulong[m_bincount + 1];
        }

        public override string ToString()
        {
            return String.Format("mean {0}, min {1}, max {2}, var {3}, total {4} ({5} samples)",
                                 Avg, Min, Max, Variance, Total, Count);
        }

        public double Total
        { get { return m_total; } }

        public double Variance
        { get { return AvgSq - Avg * Avg; } }

        public double Min
        { get { return m_count > 0 ? m_min : 0.0; } }

        public double Max
        { get { return m_count > 0 ? m_max : 0.0; } }

        public double Avg
        { get { return (m_count > 0) ? (m_total / m_count) : 0; } }

        public double AvgSq
        { get { return (m_count > 0) ? (m_sqtotal / m_count) : 0; } }

        public ulong Count
        { get { return m_count; } }

        public ulong[] Hist
        { get { return m_bins; } }

        public override void DumpJSON(TextWriter tw)
        {
            tw.Write("\n{{\"avg\":{0},\"min\":{1},\"max\":{2},\"std\":{3},\"count\":{4},\"binmin\":{5},\"binmax\":{6}",
                     Avg, Min, Max, Math.Sqrt(Variance), Count, m_binmin, m_binmax);

            if (Config.histogram_bins)
            {
                tw.Write(",\"bins\":");

                tw.Write("[");
                bool first = true;
                foreach (ulong i in m_bins)
                {
                    if (first) first = false;
                    else tw.Write(",");
                    tw.Write("{0}", i);
                }
                tw.Write("]");
            }

            tw.Write("}");
        }
    }


    // an AccumStat is a statistic that counts discrete events (flit
    // deflections, ...) and from which we can extract a rate
    // (events/time).
    public class AccumStat : StatsObject
    {
        protected double m_count;
		private ulong m_endtime;

        public AccumStat()
        {
            Reset();
        }

        public void Add()
        {
            m_count++;
        }

        public void Add(ulong addee)
        {
			m_count += (ulong) addee;
        }

		public void Add(double addee)
		{
			m_count += addee;
		}

        // USE WITH CARE. (e.g., canceling an event that didn't actually happen...)
        public void Sub()
        {
			m_count--;
        }

        public void Sub(ulong subtrahend) // I've always wanted to use that word...
        {
			m_count -= (ulong) subtrahend;
        }

		public void Sub(double subtrahend) // I've always wanted to use that word...
		{
			m_count -= subtrahend;
		}

        public override void Reset()
        {
            m_count = 0;
            m_endtime = 0;
        }

        public void Finish(ulong time)
        {
            m_endtime = time;
        }

        public override void DumpJSON(TextWriter tw)
        {
            tw.Write("{0}", Math.Round(m_count,3));
        }

        public double Count
		{ 
			get { return m_count; } 
		}

        public double Rate // events per time unit
        { get { return (double)Count / (double)m_endtime; } }
    }

    public class ConstAccumStat : AccumStat
    {
        public override void Reset() {} //can't be reset
    }





    //this metric
    public class PeriodicAccumStat : AccumStat
    {
		private List<double> history = new List<double>();
		private double emwa_factor = Config.ewmv_factor;


		public double LastPeriodValue
		{
			get{
				if (history.Count >= 1)
					return history [history.Count - 1];
				else
					return 1;
			}
		}

		public double ExpWhtMvAvg ()
		{
			double current = m_count;

			double new_ewma;
			int count = history.Count;
			double lastPeriodValue;
			if (count == 0) {
				lastPeriodValue = 0;
				new_ewma = current;
			} else {
				lastPeriodValue = history [history.Count - 1];
				new_ewma = emwa_factor * lastPeriodValue + current * (1 - emwa_factor);
			}
			history.Add (new_ewma);
			m_count = 0;
			return new_ewma;
		}
			
        public override void Reset()
        {
            base.Reset();
            history.Clear();
        }

		public double EndPeriod()
        {
            double lastPeriodValue = m_count;
            history.Add(m_count);
            m_count = 0;
            return lastPeriodValue;
        }

        public override void DumpJSON(TextWriter tw)
        {
			tw.Write (Environment.NewLine);
            tw.Write("{");
			foreach (double i in history)
				tw.Write ("{0},",Math.Round(i,3));
			tw.Write ("{0},",Math.Round(m_count,3));

			/*
            foreach (double i in history)
			{
				if (i is int || i is ulong)
					tw.Write ("{0},",i);
				else if (i is float || i is double)
                	tw.Write("{0:0.00},", i);
				else
					throw new Exception ("Dump type is undefined.");
			}
			if (m_count is int || m_count is ulong)
				tw.Write ("{0},",m_count);
			else if (m_count is float || m_count is double)
				tw.Write("{0:0.00}", m_count);
			*/
			tw.Write("}");
        }
    }
}
