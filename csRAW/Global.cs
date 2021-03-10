using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAW;

namespace csRAW
{
    static class GlobalData
    {
        public static RAWTable CreateGlobal()
        {
            RAWCSFunction printfunc = new RAWCSFunction((context, param, owner) =>
            {
                Console.WriteLine(param[0]);
                return new RAWNull();
            });

            RAWCSFunction getctxfunc = new RAWCSFunction((context, param, owner) =>
            {
                return param.Count == 0 ? context.GetLocal() : context.GetByIndex(Convert.ToInt32(param[0]));
            });

            RAWCSFunction pushctx = new RAWCSFunction((context, param, owner) =>
            {
                context.Push(param.Count != 0 ? (RAWTable)param[0] : new RAWTable());
                return new RAWNull();
            });

            RAWCSFunction popctx = new RAWCSFunction((context, param, owner) =>
            {
                context.Pop();
                return new RAWNull();
            });

            RAWCSFunction setfuncctx = new RAWCSFunction((context, param, owner) =>
            {
                ((RAWFunction)param[0]).set_ctx = (RAWTable)param[1];
                return new RAWNull();
            });

            RAWCSFunction input = new RAWCSFunction((context, param, owner) => {
                if (param.Count != 0) Console.Write(param[0]);
                return Console.ReadLine();
            });

            RAWCSFunction tonum = new RAWCSFunction((context, param, owner) =>
            {
                if (param.Count == 0) return new RAWNull();

                if (param[0] is string s)
                    return Convert.ToDouble(s);

                if (param[0] is double d)
                    return d;

                return new RAWNull();
            });

            RAWCSFunction errorfunc = new RAWCSFunction((context, param, owner) =>
            {
                if (param.Count == 0) return new RAWNull();

                throw new RuntimeError((string)param[0]);
            });

            RAWCSFunction typef = new RAWCSFunction((context, param, owner) =>
            {
                if (param.Count == 0) return new RAWNull();

                if (param[0] is string) return "str";
                if (param[0] is double) return "num";
                if (param[0] is RAWTable) return "table";
                if (param[0] is List<object>) return "array";
                if (param[0] is RAWCSFunction) return "ifunc";
                if (param[0] is RAWFunction) return "func";

                return new RAWNull();
            });

            RAWTable GlobalCTX = new RAWTable()
            {
                ["print"] = printfunc,
                ["input"] = input,
                ["num"] = tonum,
                ["error"] = errorfunc,
                ["type"] = typef,
                ["debug"] = new RAWTable()
                {
                    ["ctx"] = getctxfunc,
                    ["ctxpush"] = pushctx,
                    ["ctxpop"] = popctx,
                    ["ctxfunc"] = setfuncctx
                }
            };

            return GlobalCTX;
        }
    }
}
