#!/usr/bin/python

import sys
import os

workload_dir = "./workload_list/"
workload = "random_8x8"
out_dir_bs = "./results/random/8x8/baseline/"
out_dir_design = "./results/random/8x8/design/"

for sim_index in range(1, 31, 1):
  out_file = "sim_" + str(sim_index) + ".out"
  command_line = "mono ./sim.exe -config ./config_qos.txt -output " + out_dir_bs + out_file + " -workload " + workload_dir + workload + " " + str(sim_index) + " -throttle_enable false"
  os.system (command_line)
  if not os.path.exists(out_dir_design):
    os.makedirs(out_dir_design)
  command_line = "mono ./sim.exe -config ./config_qos.txt -output " + out_dir_design + out_file + " -workload " + workload_dir + workload + ' ' + str(sim_index)+ " -throttle_enable true -curr_L1miss_threshold 0.02 -slowdown_epoch 20000 -thrt_up_slow_app 4 -thrt_down_stc_app 14 -th_unfairness 0.32"
  os.system (command_line)





