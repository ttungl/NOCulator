using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace ICSimulator
{
    public class BitValuePair
    {
        public int bits;
        public ulong val
        {
            get { return ((ulong)1) << bits; }
            set
            {
                bits = (int)Math.Ceiling(Math.Log(value, 2));
                if ((int)Math.Floor(Math.Log(value, 2)) != bits)
                    throw new Exception("Only settable to powers of two!");
            }
        }
        public BitValuePair(int bits)
        {
            this.bits = bits;
        }
    }
    public abstract class ConfigGroup
    {
        /// <summary>
        /// Special switch for complex parameter initialization
        /// </summary>
        protected abstract bool setSpecialParameter(string param, string val);

        /// <summary>
        /// Verify member parameters; called after all options are parsed.
        /// </summary>
        public abstract void finalize();

        public void setParameter(string param, string val)
        {
            if (setSpecialParameter(param, val))
                return;
            try
            {
                FieldInfo fi = GetType().GetField(param);
                Type t = fi.FieldType;

                if (t == typeof(int))
                    fi.SetValue(this, int.Parse(val));
                else if (t == typeof(ulong))
                    fi.SetValue(this, ulong.Parse(val));
                else if (t == typeof(double))
                    fi.SetValue(this, double.Parse(val));
                else if (t == typeof(bool))
                    fi.SetValue(this, bool.Parse(val));
                else if (t == typeof(string))
                    fi.SetValue(this, val);
                else if (t.BaseType == typeof(Enum))
                    fi.SetValue(this, Enum.Parse(t, val));
                else if (t == typeof(BitValuePair))
                    ((BitValuePair)fi.GetValue(this)).bits = int.Parse(val);
                else
                    throw new Exception(String.Format("Unhandled parameter type {0}", t));
            }
            catch (NullReferenceException e)
            {
                Console.WriteLine("Parameter {0} not found!", param);
                throw e;
            }
        }
    }

    public enum BinningMethod
    {
        NONE,
        KMEANS,
        EQUAL_PER,
        DELTA
    }
	
    public enum Topology
    {
            HR_4drop,
            HR_8drop,
            HR_16drop,
            HR_8_16drop,
            HR_8_8drop,
            HR_16_8drop,
            HR_32_8drop,
            HR_buffered,
            SingleRing,
            MeshOfRings,
            Mesh,
            BufRingNetwork,
            BufRingNetworkMulti
    }


    public class Config : ConfigGroup
    {
        public static ProcessorConfig proc = new ProcessorConfig();
        public static MemoryConfig memory = new MemoryConfig();
        public static RouterConfig router = new RouterConfig();

		// ----
		// By Xiyue
		public static bool preempt = false;
		public static bool slowdown_aware = false;
		public static double preempt_threshold = 8;
		public static double slowdown_delta = 0.1; // slowdown difference between each ranking level
		public static double enable_qos_non_mem_threshold = 3; // i.e. enable_qos_non_mem_threhold * slowdown_delta
		public static double enable_qos_mem_threshold = 3;
		public static double mpki_threshold = 30;

		public static bool throttle_enable = true;
		public static double curr_L1miss_threshold = 0.06; // an app can be throttled only if L1 miss > curr_L1miss_threshlod; Greater value will preserve performance (i.e., less applciation will be throughput sensitive), but give less improvement on fairness
		public static double slowdown_epoch = 10000; // may sweep
		public static double throt_min = 0.1;  // share with Sigcomm paper
		public static int th_bad_dec_counter = 3; // may sweep
		public static double th_unfairness = 0.001; // may sweep
		public static double throt_prob_lv1 = 0.4;
		public static double throt_prob_lv2 = 0.6;
		public static double throt_prob_lv3 = 0.8;
		public static int th_bad_rst_counter = 10; //may sweep
		public static int thrt_up_slow_app = 4; // Parameter: Greater value improves fairness at the cost of lower performance improvement margin.
		public static int thrt_down_stc_app = 4; // Parameter: greater value improve fairness at the cost of performance degradation
		public static double throt_down_stage1 = 0.5;

		// Slowdown-aware Throttling
		
		public static string throttle_node = "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";
		//public static string throttle_node = "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";
		public static int throt_app_count = 4;
		public static int unthrot_app_count = 4;
		public static int opt_perf_bound = 10;
		public static int opt_fair_bound = 10;
		public static double perf_budget_factor = 0.5;
		public static double fair_budget_factor = 0.5;
		public static double th_init_perf_loss = 0.2;
		public static double th_init_fair_loss = 0.2;


		public static double sweep_th_rate = 0; 		// static throttle rate, intial value of dynamic throttle rate
		// specify which node to throttle when using static throttle controller
		public static double throt_max = 0.9;	// share with Sigcomm paper

		public static ulong STC_batchPeriod = 1000;
		public static ulong STC_batchCount = 8;
		public static double default_throttle = 0.9;
	
		public static double ewmv_factor = 0.1; // weight of old value
		public static double thrt_sweep = 0.1;
		// end Xiyue

        // ----
        // Buffered Rings (Ravindran et al, HPCA 1997)
        public static int bufrings_n = 1; // number of virtual networks
        public static int bufrings_locallat = 2; // cycles per local ring hop (router + link)
        public static int bufrings_globallat = 3; // cycles per global ring hop (router + link)
        public static int bufrings_localbuf = 4;
        public static int bufrings_globalbuf = 4;
        public static int bufrings_L2G = 4;
        public static int bufrings_G2L = 4;
        public static int bufrings_levels = 2;
        public static int bufrings_branching = 4;
        public static bool bufrings_inf_credit = false;

        // ---- GraphLabs (and other traces that has barriers at the end
        public static bool endOfTraceSync = false;
        // ---- Synthetic HPCA 13
        public static double non_local_chance = 1.0;

        // ---- MICRO'11
        
        // -- close-to-original hotnets
        public static bool hotnets_closedloop_rate = false; // false = orig. paper, true = feedback loop
        public static double hotnets_max_throttle = 0.75;
        public static double hotnets_min_throttle = 0.45;
        public static double hotnets_scale = 0.30;
        public static int hotnets_ipc_quantum = 10000;
        // also uses selftuned_quantum for either (i) rate adjustment step or (ii) throttling update step
        public static double hotnets_starve_max = 0.8;
        public static double hotnets_starve_min = 0.2;
        public static double hotnets_starve_scale = 0.35;
        public static int hotnets_starve_window = 128;
        public static int hotnets_qlen_window = 1000;

        // -- AFC
        public static int afc_buf_per_vnet = 8;
        public static int afc_vnets = 8;
        public static bool afc_real_classes = false; // otherwise, randomize for even distribution
        public static int afc_avg_window = 4;
        public static double afc_ewma_history = 0.05;
        public static double afc_buf_threshold = 0.6;
        public static double afc_bless_threshold = 0.5;
        public static bool afc_force = false; // force bufferless or buffered mode?
        public static bool afc_force_buffered = true; // if force, force buffered or bufferless?
        
        // -- self-tuned congestion control (simple netutil-based throttler)
        public static int selftuned_quantum = 128; // update once per quantum
        public static int selftuned_netutil_window = 64;
        public static bool selftuned_bangbang = false; // true = bang-bang control, false = adj. throt rate
        public static double selftuned_rate_delta = 0.01; // throt-rate delta
        public static double selftuned_netutil_tolerance = 0.05; // tolerance on either side of target (hysteresis)
        public static double selftuned_init_netutil_target = 0.7;

        // -- self-tuned congestion control: hillclimbing based on above netutil-target-seeking
        public static bool selftuned_seek_higher_ground = false; // hillclimb on IPC? (off by default)
        public static int selftuned_ipc_window = 100000;
        public static int selftuned_ipc_quantum = 100000;
        public static double selftuned_drop_threshold = 0.9; //0.75;
        public static double selftuned_target_decrease = 0.02;
        public static double selftuned_target_increase = 0.02;
        
        // ---- Global RR throttling
        public static bool cluster_prios = false;
        public static bool cluster_prios_injq = false; // extend prios into inj Q
        public static double MPKI_max_thresh = 50;
        public static double MPKI_min_thresh = 35;
        public static double MPKI_high_node = 50;
        public static int num_epoch=3;
        public static double thresholdWeight=0.1;
        public static bool canAddEmptyCluster = false;
        public static int short_life_interval=10;
        public static double throttling_threshold=2.0;
        public static double netutil_throttling_threshold=0.5;

        /* PARAMETERS THAT MATTER FOR Controller_Three_Level_Cluster_NoTrigger.cs
         * This is the final controller for MICRO */
        public static int throttle_sampling_period=100000;
        public static int interval_length=1000;
        public static double netutil_throttling_target=0.6;
        //Thorttling rate for high and always throttled cluster
        public static double RR_throttle_rate=0;
        public static double max_throttle_rate=0.95;
        //Low intensity cluster total MPKI cap
        public static double free_total_MPKI = 150;
        public static double cluster_MPKI_threshold = 50;
        public static bool always_cluster_enabled=true;
        public static bool low_cluster_filling_enabled=true;
        public static double low_apps_mpki_thresh=30;
        ///////////////////////////////////////////////////


        public static bool adaptive_cluster_mpki=false;
        public static bool alpha=false;
        public static bool adaptive_rate=false;

        // ---- Cluster will try to map far node to the same cluster
        public static bool distanceAwareCluster = true;

        // ---- Three level throttling controller
        // # of sampling periods for each applications test.
        public static int apps_test_freq=1;
        public static double sampling_period_freq=10;
        public static double sensitivity_threshold=0.05;
        // apps with high mpki that exceeds cluster threshold will be always throttled
        public static bool use_cluster_threshold=true;
        public static bool use_ipc_delta=false;

        // ---- Three level throttling controller
        // NETGAIN
        public static bool use_absolute=true;
        public static double ipc_hurt_co=1;
        public static bool gain_hurt_rr_only=false;
        public static bool ipc_hurt_neg_only=false;


        // ---- SIGCOMM'11
        //
        public static int workload_number = 0;

        public static string lambdas = "";
        public static string misses = "";

        public static bool simplemap_mem = false;
        public static int simple_MC_think = 200;

        //public static ControllerType controller = ControllerType.NEIGHBOR_LOCAL; // This is for RC or HR locality
	    //public static ControllerType controller = ControllerType.SIMPLEMAP;    // This is for mesh locality(Bless, Chipper, buffered)
	    public static ControllerType controller = ControllerType.NONE;

        public static string controller_mix = "";

        // STC:
        // Every 'reprioritizePeriod', counters are calculated for each app as
        // numerator/denom, factoring into past values based on historyWeight,
        // packets are then put into bins for injection and routing decisions

        public static double STC_history = 0;
        public static ulong STC_period = 300000;

        public static BinningMethod STC_binMethod = BinningMethod.KMEANS;
        public static int STC_binCount = 8;

        //public static ulong STC_batchPeriod = 16000;
        //public static ulong STC_batchCount = 8;

        // throttling:
        // params for HotNets
        public static int throttle_epoch = 1000;        
        public static int throttle_averaging_window = 128;
        // also throt_min, throt_max, throt_scale, congested_alpha, congested_beta, congested_gamma below



        // simple mapping (distribution):
        
        public static double simplemap_lambda = 2.0;
        public static int bounded_locality = -1;
        public static int neighborhood_locality = -1;
        public static int overlapping_squares = -1;
 
        // ----

        public static bool histogram_bins = false;

        public static int barrier = 1;

        // ---- ISCA'11
        public static int ideal_router_width = 8;
        public static int ideal_router_length = 4;
        // ---

        public static bool bochs_fe = false;

        // ---- synth traces
        public static double synth_reads_fraction = 0.8;
        public static double synth_rate = 0.1;
		public static bool bSynthBitComplement = false;
		public static bool bSynthTranspose = false;
		public static bool bSynthHotspot = false;
		public static bool randomHotspot = false;

        // ----

        // ---- sampling methodology
        public static int rand_seed = 0; // controlled seed for deterministic runs
        public static ulong randomize_trace = 0; // range of starting counts (in insns)
        public static int warmup_cyc = 0;
        public static bool trace_wraparound = false;
        public static ulong insns = 1000000000000; // insns for which stats are collected on each CPU

        // for Weighted Speedup
        public static string readlog = ""; // retire-time annotation log (one per trace, space-separated)
        public static string writelog = ""; // name of single annotation log to write
        public static int writelog_node = -1; // node from which to take annotation log
        public static string logpath = "."; // path in which to find annotation logs
        public static int writelog_delta = 100; // instruction delta at which to write retire-time annotations
 
        // ----

        // ---- golden packet / injection token
        public static bool calf_new_inj_ej = false;
        public static int gp_levels = 1;
        public static double gp_epoch = 1.0;
        public static int gp_rescuers = 0;
        public static bool gp_rescuers_dummy = false;
        public static bool gp_adaptive = false;
        public static bool dor_only = false; // dimension-ordered routing only
        public static bool edge_loop = false;
        public static bool torus = false;
        public static bool sortnet_twist = true;
        public static bool sortnet_full = false;
        // ----

        // ---- promises
        public static bool promise_wb_only = false;
        public static bool coherence_noPromises = false; // promises not used; writebacks bypass NoC
        // ----

        // ---- new cache architecture
        public static bool simple_nocoher = false; // simple no-coherence, no-memory (low overhead) model?
        public static int cache_block = 5; // cache block size is universal
        public static int coherent_cache_size = 14; // power of two
        public static int coherent_cache_assoc = 2; // power of two
        public static bool sh_cache = true;   // have a shared cache layer?
        public static int sh_cache_size = 18; // power of two: per slice
        public static int sh_cache_assoc = 4; // power of two
        public static bool sh_cache_perfect = true; // perfect shared cache?
        public static int shcache_buf = 16; // reassembly buffer slots per shared cache slice
        public static int shcache_lat = 15; // cycles
        public static int cohcache_lat = 2; // cycles
        public static int cacheop_lat = 1; // cycles -- passthrough lat for in-progress operations
        // ----

        // ---- CAL paper (true bufferless)
        public static int cheap_of = 1; // oldest-first age divisor
        public static int cheap_of_cap = -1; // if not -1, livelock limit at which we flush

        public static bool naive_rx_buf = false;
        public static bool naive_rx_buf_evict = false;
        public static RxBufMode rxbuf_mode = RxBufMode.NONE;
        public static int rxbuf_size = 8;

        public static bool split_queues = true; // for validation with orig sim
        public static int mshrs = 16;
        public static bool rxbuf_cache_only = true;
        public static bool ctrl_data_split = false;

        public static bool ignore_livelock = true;
        public static ulong livelock_thresh = 1000000;
        // ----

        
        // ---- SIGCOMM'10 paper (starvation congestion-control)
        public static bool starve_control = false;
        public static bool speriod_control = false;
        public static bool net_age_arbitration = false;
        public static bool tier1_disabled = false;
        public static bool tier2_disabled = true;
        public static bool tier3_disabled = false;
        public static double srate_thresh = 0.45;
        public static int srate_win = 1000;
        public static bool srate_log = false;
        public static bool starve_log = false;
        public static bool netu_log = false;
        public static bool nthrot_log = false;
        public static int interleave_bits = 0;
        public static bool valiant = false;
        public static bool stnetu_log = false;
        public static bool irate_log = false;
        public static bool qlen_log = false;
        public static bool aqlen_log = false;
        public static bool sstate_log = false;
        public static double static_throttle = 0.0;
        public static bool starvehigh_new = true;
        public static bool throt_ideal = false;
        public static bool throt_starvee = false;
        public static bool throt_stree = false;
        public static int throt_stree_t = 0;
        public static bool starvee_distributed = false;
        public static bool ttime_log = false;
        public static bool tier1_prob = true;
        public static bool biasing_inject = false;
        public static bool sources_log = false;
        public static bool nbutil_log = false;
        public static double bias_prob = 0.333;
        public static bool bias_single = true;
        public static string socket = "";
        public static bool idealnet = false;
        public static int ideallat = 10;
        public static int idealcap = 16;
        public static bool idealrr = false;
        public static double throt_rate = 0.75;
        public static double congested_alpha = 0.35;
        public static double congested_omega = 15;
        public static double congested_max = 0.9;
        //public static double throt_min = 0.45;
        //public static double throt_max = 0.75;
        public static double throt_scale = 10;
        public static double avg_scale = 1;
        public static bool always_obey_tier3 = true;
        public static bool use_qlen = true;
        public static int l1count_Q = 100000;
        // ----

        // ---- SCARAB impl
        public static int nack_linkLatency = 2;
        public static int nack_nr = 16;
        public static bool address_is_node = false;
        public static bool opp_buffering = false;
        public static bool nack_epoch = false;
        // ----

        public static bool progress = true;
        public static string output = "output.txt";
        public static string matlab = "";
        public static string fairdata = "";
        public static int simulationDuration = 1000;
        public static bool stopOnEnd = false;
        public static int network_nrX = 2;
        public static int network_nrY = 2;
        public static bool randomize_defl = true;
        public static int network_loopback = -1;
        public static int network_quantum = -1;
        public static int entropy_quantum = 1000; // in cycles
       // 741: BLESS-CC Congestion Avoidance Variables
        public static int BLESSCC_DeflectionAvgRounds = 10;
        public static double BLESSCC_DeflectionThreshold = 35.0;

        // 741: BLESS-CC variables for distinguishing congestion from hot-spots
        public static int BLESSCC_BitsForCongestion = 3;
        public static double BLESSCC_CongestionDecrease = 0.1;
        public static double BLESSCC_CongestionIncrease = 0.01;
        public static int BLESSCC_MinimumRoundsBetween = 5;

        // 741: BLESS-CC Valiant routing variables
        public static int BLESSCC_ValiantRounds = 50;
        public static int BLESSCC_ValiantProcessors = 4;

        // 741: BLESS-CC Fairness variables
        public static int BLESSCC_RateHistoryRounds = 20;
        public static int BLESSCC_RateHistorySources = 1;
        public static bool BLESSCC_FairnessInputOnly = false;

        // 741: Link monitoring variables to prevent starvation
        public static int BLESSCC_StarvationRounds = 1000;

        // ------ CIF: experiment parameters, new version -----------------
        public static string sources = "all uniformRandom";
        public static string finish = "cycle 10000000";
        public static string solo = "";

        public static int N
        { get { return network_nrX * network_nrY; } }

        //TODO: CATEGORIZE
        public static string[] traceFilenames;
        public static string TraceDirs = ""; ///<summary> Comma delimited list of dirs potentially containing traces </summary>
        public static bool PerfectLastLevelCache = false;

        public static bool RouterEvaluation = false;
        public static ulong RouterEvaluationIterations = 1;
        
        // Adaptive Routing
        public static bool RandOrderRouting = false;
        public static bool DeflectOrderRouting = false;
        
        // Topology        
        public static bool RingClustered = false;
        public static bool QuadParityPartition = false;
        public static bool TorusSingleRing = false;
        public static bool SourceBiasedInjection = false;
	    public static bool InverseBiasedInjection = false;
	    public static bool AgressiveInjection = false;
	    
	    //Scalable RingClustered
        public static bool ScalableRingClustered = false;
		public static bool RC_mesh = true;
		public static bool RC_Torus = false;        
		public static bool RC_x_Torus = false;
		public static bool AllBiDirLink = false;

		//Hierarchical Ring
		public static bool HR_NoBias = false;
		public static bool HR_SimpleBias = false; // flits get on the global ring only when the path it's taking is short. No bias at injection
		public static bool HR_NoBuffer = false;
		public static int G2LBufferDepth = 4;
		public static int L2GBufferDepth = 1;
		public static bool SingleDirRing = false;
		public static Topology topology = Topology.Mesh;
		public static int observerThreshold = 4;
		public static bool NoPreference = false;
		public static bool forcePreference = false;
		public static bool simpleLivelock = false;
		public static int starveThreshold = 1000;
		public static int starveDelay = 10;

		// Place holder number
		public static int PlaceHolderNum = 0;

		public static int meshEjectTrial = 1;
		public static int RingEjectTrial = -1;
		public static int RingInjectTrial = 1;
		public static int EjectBufferSize = -1;
		public static bool InfEjectBuffer = false;
		public static int GlobalRingWidth = 2;

		// Buffered Hierarchical ring
		public static bool bBufferedRing = false;
		public static int ringBufferSize = 4;
		public static int HRBufferDepth = 4;

        public void read(string[] args)
        {
            string[] traceArgs = null;
            int traceArgOffset = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-workload")
                {
                    string worksetFile = args[i + 1];
                    int index = int.Parse(args[i + 2]);
                    workload_number = index;
                    if (!File.Exists(args[i + 1]))
                        throw new Exception("Could not locate workset file " + worksetFile);
                    string[] lines = File.ReadAllLines(worksetFile);
                    if (TraceDirs == "")
                        TraceDirs = lines[0];
                    traceArgs = lines[index].Split();
                    traceArgOffset = 0;
                    i += 2;
                }
                else if (args[i] == "-traces")
                {
                    traceArgs = args;
                    traceArgOffset = i + 1;
                    break;
                }
                else
                {
                    setSystemParameter(args[i].Substring(1), args[i + 1]);
                    i++;
                }
            }
			// by Xiyue: check the number of trace read in; Must equal to the number of nodes. 
			//           if there is no workload running on a node, must specify null in the workload list (-workload).
			traceFilenames = new string[N];
            if (traceArgs.Length - traceArgOffset < N)
                throw new Exception(
                    String.Format("Not enough trace files given (got {0}, wanted {1})", traceArgs.Length - traceArgOffset, N));

			for (int a = 0; a < N; a++)
            {
				traceFilenames [a] = traceArgs [traceArgOffset + a];
            }

            Simulator.stats = new Stats(Config.N);  // by Xiyue: statics entry

            finalize();
            proc.finalize();
            memory.finalize();
            router.finalize();
        }
        public void readConfig(string filename)
        {
            Char[] delims = new Char[] { '=' }; //took out ' ' for better listing capabilities

			StreamReader configReader = File.OpenText(filename);

            if (configReader == null)
                return;

            string buf;
            for (; ; )
            {
                buf = configReader.ReadLine();
                if (buf == null)
                    break;

                int comment = buf.IndexOf("//");
                if (comment != -1)
                    buf = buf.Remove(comment).Trim();

                if (buf == "")
                    continue;

                string[] flags = buf.Split(delims, 2);
                if (flags.Length < 2) continue;
                setSystemParameter(flags[0].Trim(), flags[1].Trim());
            }
        }

        public void setSystemParameter(string param, string val)
        {
            Console.WriteLine("{0} <= {1}", param, val);
            if (param.Contains("."))
            {
                string name = param.Substring(0, param.IndexOf('.'));
                string subparam = param.Substring(param.IndexOf('.') + 1);
                FieldInfo fi = GetType().GetField(name);
                if (!(fi.GetValue(this) is ConfigGroup))
                {
                    throw new Exception(String.Format("Non-ConfigGroup indexed, of type {0}", fi.FieldType.ToString()));
                }
                ((ConfigGroup)fi.GetValue(this)).setParameter(subparam, val);
            }
            else
                setParameter(param, val);
        }
        protected override bool setSpecialParameter(string param, string val)
        {
            switch (param)
            {
                case "config":
                    readConfig(val); break;
                default:
                    return false;
            }
            return true;
        }

        public static string ConfigHash()
        {
            System.Security.Cryptography.MD5CryptoServiceProvider md5 =
                new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] checksum = md5.ComputeHash(new System.Text.ASCIIEncoding().GetBytes(
                                                  Config.sources +
                                                  Config.router.algorithm +
                                                  Config.router.options +
                                                  Config.network_nrX + Config.network_nrY));
            return BitConverter.ToString(checksum).Replace("-", "");
        }

        public override void finalize()
        {
            if (output == "" && matlab == "")
            {
                throw new Exception("No output specified.");
            }

            if (STC_binCount == 0)
                STC_binCount = N;
        }
    }
}
