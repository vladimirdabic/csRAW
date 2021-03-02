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
            if(args.Length == 0)
            {
                Console.WriteLine("Please enter a script file name");
                Console.WriteLine("Usage: RAW <script file>");
                return;
            }

            if(!File.Exists(args[0]))
            {
                Console.WriteLine($"File '{args[0]}' not found");
                return;
            }

            string CodeToRun = File.ReadAllText(args[0]);

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
        }
    }
}
