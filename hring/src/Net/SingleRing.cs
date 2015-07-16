#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class SingleRing_Network : Network
    {
        public SingleRing_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }
      	public override void setup()
       	{
            if (Config.bBufferedRing)
                nodeRouters = new Router_Node_Buffer[Config.N];
            else
                nodeRouters = new Router_Node[Config.N];

            nodes = new Node[Config.N];
            links = new List<Link>();
            cache = new CmpCache();

            ParseFinish(Config.finish);

            workload = new Workload(Config.traceFilenames);

            mapping = new NodeMapping_AllCPU_SharedCache();

            // create routers and nodes
            for (int n = 0; n < Config.N; n++)
           	{
                Coord c = new Coord(n);
                RC_Coord RC_c = new RC_Coord(n);

                nodes[n] = new Node(mapping, c);
                if (Config.bBufferedRing)
                    nodeRouters[n] = new Router_Node_Buffer(RC_c, c);
                else
                    nodeRouters[n] = new Router_Node(RC_c, c);
                nodes[n].setRouter(nodeRouters[n]);
                nodeRouters[n].setNode(nodes[n]);
            }
            // connect the network with Links
            for (int n = 0; n < Config.N; n++)
            {
				int next = (n + 1) % Config.N;
				Link dirA = new Link(Config.router.switchLinkLatency - 1);
				Link dirB = new Link(Config.router.switchLinkLatency - 1);
				links.Add(dirA);
				links.Add(dirB);
				nodeRouters[n].linkOut[CW] = dirA;
				nodeRouters[next].linkIn[CW] = dirA;
				nodeRouters[n].linkIn[CCW] = dirB;
				nodeRouters[next].linkOut[CCW] = dirB;
			}
       	}

        public override void doStep()
        {
        
            doStats();
			for (int n = 0; n < Config.N; n++)
				nodes[n].doStep();
           	for (int n = 0; n < Config.N; n++)
           		nodeRouters[n].doStep();
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
		}

		void printFlits()
		{
			for (int i = 0; i < Config.N; i++)
			{
				int from , to;
				Flit f = nodeRouters[i].linkIn[CW].Out;
				if (f == null)  {from = -1; to = -1;}
				else {from = f.packet.src.ID; to = f.packet.dest.ID;}
				Console.WriteLine("nodeID:{0} from {1} to {2}", i, from, to);
			}
			for (int i = 0; i < Config.N; i++)
			{
				for (int dir = 2; dir <= 3; dir++)
				{
					int from, to;
					Flit f = switchRouters[i].linkIn[dir].Out;
					if (f==null) {from = -1; to = -1;}
					else {from = f.packet.src.ID; to = f.packet.dest.ID;}
					Console.WriteLine("SwitchID:{0}, from{1}, to{2}", i, from, to);
				}
			}
		}
    }
}
