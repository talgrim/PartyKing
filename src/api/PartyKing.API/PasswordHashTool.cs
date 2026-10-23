using PartyKing.Application.Admin.Services;

namespace PartyKing.API;

/// <summary>Command line helper that turns a password into the value for AdminSettings:PasswordHash.</summary>
public static class PasswordHashTool
{
    public static int Run(string[] args)
    {
        // Prefer the prompt: a password passed as an argument ends up in the shell history
        var password = args.Length > 0 ? args[0] : Prompt();

        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("The password must not be empty");
            return 1;
        }

        Console.WriteLine(new PasswordHasher().Hash(password));
        return 0;
    }

    private static string Prompt()
    {
        Console.Error.Write("Password: ");

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var password = new System.Text.StringBuilder();
        ConsoleKeyInfo key;

        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }

        Console.Error.WriteLine();
        return password.ToString();
    }
}
