using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAW;
using System.IO;


namespace csRAW
{
    class Program
    {
        static void Main(string[] args)
        {
            string filename;
            if (args.Length == 0)
            {
                if (!File.Exists(@"C:\Users\Vladimir\source\repos\csRAW\csRAW\TestCode.raw"))
                {
                    Console.WriteLine("Please enter a script file name");
                    Console.WriteLine("Usage: csRAW <script file>");
                    return;
                }

                filename = @"C:\Users\Vladimir\source\repos\csRAW\csRAW\TestCode.raw";
            }
            else
                filename = args[0];

            if(!File.Exists(filename))
            {
                Console.WriteLine($"File '{filename}' not found");
                return;
            }

            string CodeToRun = File.ReadAllText(filename);

            try
            {


                Scanner scanner = new Scanner(CodeToRun);
                List<Token> tokens = scanner.scanTokens();

                Context ctx = new Context(GlobalData.CreateGlobal());
                Parser parser = new Parser(tokens);
                Node top_node = parser.parse();

                top_node.evaluate(ctx);
            }
            catch (ScannerError e)
            {
                Console.WriteLine($"Scanner Error:\n{e.Message}");
            }
            catch (ParserError e)
            {
                Console.WriteLine($"Parser Error:\n{e.Message}");
            }
            catch (RuntimeError e)
            {
                Console.WriteLine($"Runtime Error:\n{e.Message}");
            }

            Console.ReadKey();
        }
    }
}
