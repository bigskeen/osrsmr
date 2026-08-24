package com.osrsmr.attach;

import com.sun.tools.attach.VirtualMachine;
import java.io.File;

public class AttachHelper {
    public static void main(String[] args) {
        if (args.length < 2) {
            System.err.println("Usage: AttachHelper <pid> <agent-jar-path>");
            System.exit(1);
        }

        String pid = args[0];
        String agentPath = new File(args[1]).getAbsolutePath();

        try {
            System.out.println("[ATTACH_INFO] Attaching to PID: " + pid);
            System.out.println("[ATTACH_INFO] Agent Path: " + agentPath);
            
            VirtualMachine vm = VirtualMachine.attach(pid);
            System.out.println("[ATTACH_INFO] Attached to VM. Loading agent...");
            try {
                vm.loadAgent(agentPath);
                System.out.println("[ATTACH_SUCCESS] Agent loaded successfully.");
            } catch (Exception e) {
                System.err.println("[ATTACH_ERROR] loadAgent note: " + e.getMessage());
                String msg = e.getMessage() != null ? e.getMessage().toLowerCase() : "";
                if (msg.contains("agent_onattach") || msg.contains("already loaded") || msg.contains("0")) {
                    System.out.println("[ATTACH_SUCCESS] Agent already active.");
                } else {
                    vm.loadAgent(new File(agentPath).getAbsolutePath());
                    System.out.println("[ATTACH_SUCCESS] Agent loaded on second attempt.");
                }
            }
            try {
                vm.detach();
                System.out.println("[ATTACH_SUCCESS] Detached from VM.");
            } catch (Exception ignored) {}
            
            System.out.println("[ATTACH_DONE] Process completed successfully for PID " + pid);
            System.exit(0);
        } catch (com.sun.tools.attach.AttachNotSupportedException e) {
            System.err.println("[ATTACH_FATAL] Attach not supported for PID " + pid + ": " + e.getMessage());
            e.printStackTrace();
            System.exit(2);
        } catch (Exception e) {
            System.err.println("[ATTACH_FATAL] Unexpected error during attach (PID " + pid + "): " + e.getMessage());
            e.printStackTrace();
            System.exit(1);
        }
    }
}
