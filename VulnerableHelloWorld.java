import java.io.BufferedReader;
import java.io.InputStreamReader;

public class VulnerableHelloWorld {
    public static void main(String[] args) {
        if (args.length > 0) {
            String name = args[0];
            System.out.println("Hello, " + name + "!");
            try {
                // Vulnerable part: executing system command with user input
                String command = "echo " + name;
                Process process = Runtime.getRuntime().exec(command);
                BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
                String line;
                while ((line = reader.readLine()) != null) {
                    System.out.println(line);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        } else {
            System.out.println("Hello, World!");
        }
    }
}
