using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAW
{ 

    abstract class RAWValue {
        //public abstract bool BooleanRepr();
        public abstract bool IsEqual(object other);
    }

    abstract class RAWCallable : RAWValue
    {
        public abstract object Run(Context ctx, List<object> args);
    }
    
    class RAWNull : RAWValue
    {
        public override bool IsEqual(object other)
        {
            return (other is RAWNull);
        }

        public override string ToString()
        {
            return "<raw null>";
        }
    }
    class RAWTable : RAWValue
    {
        public Dictionary<object, object> data = new Dictionary<object, object> { };
        //private RAWTable metatable;

        public RAWTable(Dictionary<object, object> data = null)
        {
            if (data != null) this.data = data;
        }

        public RAWTable Copy()
        {
            Dictionary<object, object> newdata = new Dictionary<object, object>();
            foreach(KeyValuePair<object, object> item in data)
            {
                newdata[HelperMethods.CopyObject(item.Key)] = HelperMethods.CopyObject(item.Value);
            }
            return new RAWTable(newdata);
        }
        public void Set(object key, object value)
        {
            data[key] = value;
        }

        public object Get(object key)
        {
            return data.ContainsKey(key) ? data[key] : new RAWNull();
        }

        public bool Exists(object key)
        {
            return data.ContainsKey(key);
        }

        public object this[object key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public override string ToString()
        {
            if (data.Count == 0)
                return "{}";

            string repr = "{";
            foreach(KeyValuePair<object, object> entry in data)
            {
                repr += entry.Key.ToString() + " = " + entry.Value.ToString() + ", ";
            }
            return repr.Substring(0, repr.Length-2) + "}";
        }

        public override bool IsEqual(object other)
        {
            return (other is RAWTable) && (other == this);
        }
    }

    class RAWFunction : RAWCallable
    {
        private BlockNode code;
        private List<string> arg_names;
        public RAWTable set_ctx = null;
        public object self_reference = null;

        public RAWFunction(BlockNode code, List<string> arg_names) { this.code = code; this.arg_names = arg_names; }

        public override bool IsEqual(object other)
        {
            return (other is RAWFunction) && (other == this);
        }

        public RAWFunction Copy()
        {
            return new RAWFunction(code, arg_names);
        }

        public override object Run(Context ctx, List<object> args)
        {

            RAWTable ctx_tbl = set_ctx == null ? new RAWTable(): set_ctx;
            ctx_tbl["this"] = self_reference == null ? new RAWNull() : self_reference;

            RAWTable argstbl = new RAWTable();

            for (double i = 0; i < args.Count; i++)
                argstbl[i] = args[(int)i];

            ctx_tbl["__args__"] = argstbl;

            for(int i = 0; i < arg_names.Count; i++)
                ctx_tbl[arg_names[i]] = i >= args.Count ? new RAWNull() : args[i];


            ctx.Push(ctx_tbl);
            try
            {
                code.evaluate(ctx);
            }
            catch (ReturnError e)
            {
                ctx.Pop();
                return e.ReturnValue;
            }

            ctx.Pop();
            return new RAWNull();
        }
    }

    

    class RAWCSFunction : RAWCallable
    {
        private Func<Context, List<object>, object, object> function;
        public object owner;

        public RAWCSFunction(Func<Context, List<object>, object, object> func, object owner = null)
        {
            this.owner = owner;
            function = func;
        }

        public override bool IsEqual(object other)
        {
            return (other is RAWCSFunction) && (other == this);
        }

        public override object Run(Context ctx, List<object> args)
        {
            return function(ctx, args, owner);
        }
    }
}
