using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace csRAW.RAW
{
    class Context
    {
        private List<RAWTable> context = new List<RAWTable>();

        public Context(RAWTable global = null)
        {
            Push(global);
        }

        public RAWTable GetGlobal()
        {
            return context[0];
        }

        public RAWTable GetLocal()
        {
            return context[context.Count - 1];
        }

        public RAWTable GetByIndex(int idx)
        {
            return context[idx];
        }

        public void Push(RAWTable ctx = null)
        {
            context.Add(ctx == null ? new RAWTable() : ctx);
        }

        public void Pop()
        {
            context.RemoveAt(context.Count - 1);
        }

        public object GetVar(object var_name)
        {
            object lcl = GetLocal()[var_name];
            return lcl is RAWNull ? GetGlobal()[var_name] : lcl;
        }

        public void SetVar(object var_name, object value)
        {
            RAWTable table = GetGlobal().Exists(var_name) ? GetGlobal() : GetLocal();
            table[var_name] = value;
        }
    }
}
