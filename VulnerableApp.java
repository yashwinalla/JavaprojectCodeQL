import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.ResultSet;
import java.sql.Statement;
import java.util.Scanner;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

public class VulnerableApp {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);

        // SQL Injection vulnerability
        System.out.print("Enter username: ");
        String username = scanner.nextLine();
        System.out.print("Enter password: ");
        String password = scanner.nextLine();
        login(username, password);

        // Command Injection vulnerability
        System.out.print("Enter a command to execute: ");
        String command = scanner.nextLine();
        executeCommand(command);

        // XSS vulnerability
        System.out.print("Enter your name: ");
        String name = scanner.nextLine();
        displayGreeting(name);

        // Insecure File Handling vulnerability
        System.out.print("Enter the filename to save your name: ");
        String filename = scanner.nextLine();
        saveNameToFile(name, filename);

        scanner.close();
    }

    // SQL Injection vulnerability
    public static void login(String username, String password) {
        try {
            Connection conn = DriverManager.getConnection("jdbc:mysql://localhost:3306/testdb", "root", "password");
            Statement stmt = conn.createStatement();
            String query = "SELECT * FROM users WHERE username = '" + username + "' AND password = '" + password + "'";
            ResultSet rs = stmt.executeQuery(query);
            if (rs.next()) {
                System.out.println("Login successful!");
            } else {
                System.out.println("Invalid username or password.");
            }
            conn.close();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    // Command Injection vulnerability
    public static void executeCommand(String command) {
        try {
            Process process = Runtime.getRuntime().exec(command);
            process.waitFor();
            System.out.println("Command executed.");
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    // XSS vulnerability
    public static void displayGreeting(String name) {
        System.out.println("Hello, " + name + "!");
    }

    // Insecure File Handling vulnerability
    public static void saveNameToFile(String name, String filename) {
        try {
            FileWriter writer = new FileWriter(new File(filename));
            writer.write("Name: " + name);
            writer.close();
            System.out.println("Name saved to file.");
        } catch (IOException e) {
            e.printStackTrace();
        }
    }
}
