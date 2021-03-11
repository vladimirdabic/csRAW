using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace RAW
{

    public class ReturnError : Exception
    {
        public object ReturnValue;
        public ReturnError(object value)
        {
            ReturnValue = value;
        }
    }

    public class RuntimeError : Exception
    {
        public RuntimeError(string message) : base(message) { }
        public RuntimeError(string message, Exception inner) : base(message, inner) { }
    };

    public static class HelperMethods
    {
        public static object CopyObject(object o)
        {
            if(o is RAWTable t)
                return t.Copy();
            if(o is List<object> l)
            {
                List<object> newList = new List<object>();
                foreach(object obj in l)
                {
                    newList.Add(CopyObject(obj));
                }
                return newList;
            }
            if(o is RAWFunction f)
                return f.Copy();

            return o;
        }
    }

    abstract class Node
    {
        public abstract object evaluate(Context ctx);
    }

    class LiteralNode : Node
    {
        object value;

        public LiteralNode(object value)
        {
            this.value = value;
        }

        public override object evaluate(Context ctx)
        {
            return value;
        }
    }

    class VariableNode : Node
    {
        public Token variable;
        public bool is_global;

        public VariableNode(Token variable, bool is_global) { this.variable = variable; this.is_global = is_global; }
        public override object evaluate(Context ctx)
        {
            if (is_global) return ctx.GetGlobalVar(variable.lexeme);
            return ctx.GetVar(variable.lexeme);
        }
    }

    class BinaryOpNode : Node
    {
        Node left;
        Node right;
        Token op;

        public static Dictionary<TokenType, Func<Node, Node, Context, object>> op_map = new Dictionary<TokenType, Func<Node, Node, Context, object>>
        {
            { TokenType.PLUS, (l, r, ctx) => (dynamic)l.evaluate(ctx) + (dynamic)r.evaluate(ctx) },
            { TokenType.MINUS, (l, r, ctx) => (dynamic)l.evaluate(ctx) - (dynamic)r.evaluate(ctx) },
            { TokenType.STAR, (l, r, ctx) => (dynamic)l.evaluate(ctx) * (dynamic)r.evaluate(ctx) },
            { TokenType.SLASH, (l, r, ctx) => (dynamic)l.evaluate(ctx) / (dynamic)r.evaluate(ctx) },
            { TokenType.GREATER, (l, r, ctx) => (dynamic)l.evaluate(ctx) > (dynamic)r.evaluate(ctx) },
            { TokenType.GREATER_EQUAL, (l, r, ctx) => (dynamic)l.evaluate(ctx) >= (dynamic)r.evaluate(ctx) },
            { TokenType.LESS, (l, r, ctx) => (dynamic)l.evaluate(ctx) < (dynamic)r.evaluate(ctx) },
            { TokenType.LESS_EQUAL, (l, r, ctx) => (dynamic)l.evaluate(ctx) <= (dynamic)r.evaluate(ctx) },
            { TokenType.AND, (l, r, ctx) => (dynamic)l.evaluate(ctx) && (dynamic)r.evaluate(ctx) },
            { TokenType.OR, (l, r, ctx) => (dynamic)l.evaluate(ctx) || (dynamic)r.evaluate(ctx) },
            { TokenType.EQUAL_EQUAL, (l, r, ctx) => {
                object left = l.evaluate(ctx);
                object right = r.evaluate(ctx);
                if(left is RAWValue)
                    return ((RAWValue)left).IsEqual(right);
                else if(right is RAWValue)
                    return ((RAWValue)right).IsEqual(left);

                return (dynamic)left == (dynamic)right;
            } },
            { TokenType.BANG_EQUAL, (l, r, ctx) => {
                return !(bool)op_map[TokenType.EQUAL_EQUAL](l, r, ctx);
            } }
        };

        public BinaryOpNode(Node left, Node right, Token op)
        {
            this.left = left;
            this.right = right;
            this.op = op;
        }

        public override object evaluate(Context ctx)
        {
            return op_map[op.type](left, right, ctx);
        }
    }

    class TableSetNode : Node
    {
        Token set_name;
        Node value;
        Node assignval;

        public TableSetNode(Node assignval, Token set_name, Node value) { this.value = value; this.set_name = set_name; this.assignval = assignval; }
        public override object evaluate(Context ctx)
        {
            object val = assignval.evaluate(ctx);

            if (!(val is RAWTable))
                throw new RuntimeError("Tried assinging a property to a non table value");

            RAWTable tbl_val = ((RAWTable)val);

            if (tbl_val[set_name.lexeme] is GetterSetter g && g.setter != null)
                g.Set(val, ctx, value.evaluate(ctx));
            else
                tbl_val[set_name.lexeme] = value.evaluate(ctx);

            return null;
        }
    }

    class TableSetExprNode : Node
    {
        Node set_name;
        Node value;
        Node assignval;

        public TableSetExprNode(Node assignval, Node set_name, Node value) { this.value = value; this.set_name = set_name; this.assignval = assignval; }
        public override object evaluate(Context ctx)
        {
            object val = assignval.evaluate(ctx);

            if (!(val is RAWTable) && !(val is List<object>))
                throw new RuntimeError("Tried assinging a property to a non table/array value");

            if(val is RAWTable) ((RAWTable)val)[set_name.evaluate(ctx)] = value.evaluate(ctx);
            else ((List<object>)val)[Convert.ToInt32(set_name.evaluate(ctx))] = value.evaluate(ctx);

            return null;
        }
    }

    class TableGetNode : Node
    {
        public Token get_name;
        public Node value;
        public bool pass_self;

        public TableGetNode(Node value, Token get_name, bool pass_self = false) { this.value = value; this.get_name = get_name; this.pass_self = pass_self; }
        public override object evaluate(Context ctx)
        {
            object val = value.evaluate(ctx);
            if (val is RAWTable)
            {
                object value = ((RAWTable)val)[get_name.lexeme];

                if (value is RAWFunction)
                    ((RAWFunction)value).self_reference = val;

                else if (value is GetterSetter g)
                    if (g.getter != null) return g.Get(val, ctx);
                    else return new RAWNull();

                return value;
            }
            if(val is string)
            {
                switch(get_name.lexeme)
                {
                    case "size":
                    case "len":
                    case "length":
                    case "count":
                        return ((string)val).Length;

                    case "chars":
                        List<object> chrs = new List<object>();
                        chrs.AddRange(((string)val).Select(c => c.ToString()));
                        return chrs;

                    case "sub":
                        return new RAWCSFunction((context, param, owner) => {
                            if (param.Count == 0) return new RAWNull();

                            int start = Convert.ToInt32(param[0]);
                            if (param.Count == 1)
                                return ((string)owner).Substring(start);

                            int end = Convert.ToInt32(param[1]);
                            return ((string)owner).Substring(start, end-start);
                        }, val);

                    case "match":
                        return new RAWCSFunction((context, param, owner) => {
                            if (param.Count == 0) return new RAWNull();

                            Match match;

                            try
                            {
                                match = Regex.Match((string)owner, (string)param[0]);
                            } catch
                            {
                                return new RAWNull();
                            }

                            return match.Success;
                        }, val);
                    case "replace":
                        return new RAWCSFunction((context, param, owner) => {
                            if (param.Count < 2) return owner;

                            owner = ((string)owner).Replace((string)param[0], (string)param[1]);

                            return owner;
                        }, val);
                }
            }
            if(val is List<object>)
            {
                switch(get_name.lexeme)
                {
                    case "size":
                    case "len":
                    case "length":
                    case "count":
                        return ((List<object>)val).Count;

                    case "add":
                        return new RAWCSFunction((context, param, owner) => {
                            if (param.Count == 0) return new RAWNull();

                            ((List<object>)owner).Add(param[0]);

                            return new RAWNull();
                        }, val);

                    case "pop":
                        return new RAWCSFunction((context, param, owner) => {
                            if (param.Count == 0) return new RAWNull();

                            if(param[0] is double)
                            {
                                object validx = ((List<object>)owner)[Convert.ToInt32(param[0])];
                                ((List<object>)owner).RemoveAt(Convert.ToInt32(param[0]));
                                return validx;
                            }

                            return new RAWNull();
                        }, val);

                    case "clear":
                        return new RAWCSFunction((context, param, owner) => {
                            ((List<object>)owner).Clear();

                            return new RAWNull();
                        }, val);
                }
            }
            return new RAWNull();
        }
    }

    class TableGetExprNode : Node
    {
        public Node get_expr;
        public Node value;

        public TableGetExprNode(Node value, Node get_expr) { this.value = value; this.get_expr = get_expr; }
        public override object evaluate(Context ctx)
        {
            object val = value.evaluate(ctx);
            if (val is RAWTable)
            {
                object value = ((RAWTable)val)[get_expr.evaluate(ctx)];

                if (value is RAWFunction)
                    ((RAWFunction)value).self_reference = val;
                else if (value is GetterSetter g)
                    if (g.getter != null) return g.Get(val, ctx);
                    else return new RAWNull();

                return value;
            }
            if(val is List<object>)
            {
                int idx = Convert.ToInt32(get_expr.evaluate(ctx));
                if (idx >= ((List<object>)val).Count || idx < 0) return new RAWNull();
                object value = ((List<object>)val)[idx];

                if (value is RAWFunction)
                    ((RAWFunction)value).self_reference = val;

                return value;
            }
            if(val is string)
            {
                int idx = Convert.ToInt32(get_expr.evaluate(ctx));
                if (idx >= ((string)val).Length || idx < 0) return new RAWNull();
                char value = ((string)val)[idx];

                return value.ToString();
            }
            return new RAWNull();
        }
    }

    class BlockNode : Node
    {
        private List<Node> statements;

        public BlockNode(List<Node> statements)
        {
            this.statements = statements;
        }

        public override object evaluate(Context ctx)
        {
            foreach (Node node in statements) node.evaluate(ctx);
            return null;
        }
    }


    class MainContainer : Node
    {
        private BlockNode statements;
        private bool top_level;

        public MainContainer(BlockNode statements, bool top_level = false)
        {
            this.statements = statements;
            this.top_level = top_level;
        }

        public override object evaluate(Context ctx)
        {
            if (!top_level) ctx.Push();
            try
            {
                statements.evaluate(ctx);
            }
            catch (ReturnError e)
            {
                if (!top_level) ctx.Pop();
                return e.ReturnValue;
            }

            if (!top_level) ctx.Pop();
            return new RAWNull();
        }
    }

    class CTXContainer : Node
    {
        private BlockNode statements;
        private bool top_level;

        public CTXContainer(BlockNode statements, bool top_level = false)
        {
            this.statements = statements;
            this.top_level = top_level;
        }

        public override object evaluate(Context ctx)
        {
            if (!top_level) ctx.Push();
            statements.evaluate(ctx);
            if (!top_level) ctx.Pop();
            return null;
        }
    }

    class CallFunctionNode : Node
    {
        private Node value_to_call;
        private List<Node> args;

        public CallFunctionNode(Node value_to_call, List<Node> args)
        {
            this.value_to_call = value_to_call;
            this.args = args;
        }

        public override object evaluate(Context ctx)
        {
            List<object> parsedargs = new List<object>();
            foreach (Node arg in args) parsedargs.Add(arg.evaluate(ctx));

            object funcobj = value_to_call.evaluate(ctx);

            if (funcobj is RAWCallable)
                return ((RAWCallable)funcobj).Run(ctx, parsedargs);

            // if(funcobj is RAWCSFunction)
            //return ((RAWCSFunction)funcobj)(parsedargs, ctx);

            throw new RuntimeError("Tried calling a non callable value.");
        }
    }

    class ReturnNode : Node
    {
        private Node expr;

        public ReturnNode(Node expr)
        {
            this.expr = expr;
        }

        public override object evaluate(Context ctx)
        {
            throw new ReturnError(expr != null ? expr.evaluate(ctx) : new RAWNull());
        }
    }

    class AssignNode : Node
    {
        private Token varname;
        private Node expr;
        private bool global;

        public AssignNode(Token varname, Node expr, bool global)
        {
            this.varname = varname;
            this.expr = expr;
            this.global = global;
        }

        public override object evaluate(Context ctx)
        {
            object val = expr.evaluate(ctx);
            object cur_val = global ? ctx.GetGlobalVar(varname.lexeme) : ctx.GetVar(varname.lexeme);

            if(cur_val is RAWTable t)
                if(t.Exists("__assign__") && t["__assign__"] is RAWFunction)
                {
                    RAWFunction assign_func = (RAWFunction)t["__assign__"];
                    assign_func.self_reference = t;
                    assign_func.Run(ctx, new List<object> { val });
                    return val;
                }

            if (!global) ctx.SetVar(varname.lexeme, val);
            else ctx.SetGlobalVar(varname.lexeme, val);
            return val;
        }
    }

    class PassNode : Node
    {
        private Token varname;
        private MainContainer statements;

        public PassNode(Token varname, MainContainer statements)
        {
            this.varname = varname;
            this.statements = statements;

        }

        public override object evaluate(Context ctx)
        {
            ctx.SetVar(varname.lexeme, statements.evaluate(ctx));
            return null;
        }
    }


    class TableKVNode : Node
    {
        private Node key;
        private Node value;

        public TableKVNode(Node key, Node value)
        {
            this.key = key;
            this.value = value;
        }

        public override object evaluate(Context ctx)
        {
            return new List<object> { key.evaluate(ctx), value.evaluate(ctx) };
        }
    }

    class TableNode : Node
    {
        private List<TableKVNode> kvPairs;

        public TableNode(List<TableKVNode> kvPairs)
        {
            this.kvPairs = kvPairs;
        }

        public override object evaluate(Context ctx)
        {
            RAWTable tbl = new RAWTable();

            foreach (TableKVNode kvPair in kvPairs)
            {
                List<object> data = (List<object>)kvPair.evaluate(ctx);
                tbl[data[0]] = data[1];
            }

            return tbl;
        }
    }

    class GlobalDeclNode : Node
    {
        private Token var;

        public GlobalDeclNode(Token var) { this.var = var; }

        public override object evaluate(Context ctx)
        {
            ctx.GetGlobal().Set(var.lexeme, new RAWNull());
            return null;
        }
    }

    class IFNode : Node
    {
        private Node statement;
        private Node expr;

        public IFNode(Node expr, Node statement)
        {
            this.expr = expr;
            this.statement = statement;
        }

        public override object evaluate(Context ctx)
        {
            object result = expr.evaluate(ctx);

            if (result is RAWNull) return null;
            if (result is bool && !(bool)result) return null;

            statement.evaluate(ctx);
            return null;
        }
    }

    class WhileNode : Node
    {
        private Node statement;
        private Node expr;

        public WhileNode(Node expr, Node statement)
        {
            this.expr = expr;
            this.statement = statement;
        }

        public override object evaluate(Context ctx)
        {
            object result;
            do
            {
                result = expr.evaluate(ctx);

                if (result is RAWNull) return null;
                if (result is bool && !(bool)result) return null;

                statement.evaluate(ctx);
            } while (true);
        }
    }

    class FuncDefNode : Node
    {
        private Token func_name;
        private List<string> arg_ids;
        private BlockNode code;

        public FuncDefNode(Token func_name, List<string> arg_ids, BlockNode code)
        {
            this.func_name = func_name;
            this.arg_ids = arg_ids;
            this.code = code;
        }

        public override object evaluate(Context ctx)
        {
            ctx.GetLocal()[func_name.lexeme] = new RAWFunction(code, arg_ids);
            return null;
        }
    }

    class FORNode : Node
    {
        private Node statement, start, end;
        private Token varname;

        public FORNode(Token varname, Node start, Node end, Node statement)
        {
            this.varname = varname;
            this.statement = statement;
            this.start = start;
            this.end = end;
        }

        public override object evaluate(Context ctx)
        {
            for(double i = (double)start.evaluate(ctx); i < Convert.ToDouble(end.evaluate(ctx)); i++)
            {
                ctx.GetLocal()[varname.lexeme] = i;
                statement.evaluate(ctx);
            }

            return null;
        }
    }

    class VarIncDec : Node
    {
        private Token varname;
        private bool sub;
        private bool first;

        public VarIncDec(Token varname, bool sub, bool first)
        {
            this.varname = varname;
            this.sub = sub;
            this.first = first;
        }

        public override object evaluate(Context ctx)
        {
            RAWTable localctx = ctx.GetLocal();
            object value = localctx[varname.lexeme];

            if (!(value is double))
                throw new RuntimeError("Tried incrementing a non number value");

            double valued = (double)value;
            double val_ret;

            if(first)
                val_ret = sub ? --valued : ++valued;
            else
                val_ret = sub ? valued-- : valued++;

            localctx[varname.lexeme] = valued;

            return val_ret;
        }
    }

    class ArrayNode : Node
    {
        private List<Node> elements;

        public ArrayNode(List<Node> elements) { this.elements = elements; }

        public override object evaluate(Context ctx)
        {
            List<object> arr = new List<object>();

            foreach (Node n in elements)
                arr.Add(n.evaluate(ctx));

            return arr;
        }
    }

    class FOREachNode : Node
    {
        private Node statement, loop_obj;
        private Token varname;

        public FOREachNode(Token varname, Node loop_obj, Node statement)
        {
            this.varname = varname;
            this.statement = statement;
            this.loop_obj = loop_obj;
        }

        public override object evaluate(Context ctx)
        {


            object lobj = loop_obj.evaluate(ctx);

            if (lobj is List<object>)
            {
                foreach (object o in (List<object>)lobj)
                {
                    ctx.GetLocal()[varname.lexeme] = o;
                    statement.evaluate(ctx);
                }
                return null;
            }

            throw new RuntimeError("Tried looping over a non array value.");
        }
    }

    class NOTNode : Node
    {
        private Node val;

        public NOTNode(Node val) { this.val = val; }

        public override object evaluate(Context ctx)
        {
            object o = val.evaluate(ctx);

            if(o is bool b)
                return !b;

            if(o is RAWNull)
                return true;

            return false;
        }
    }

    class UMinusNode : Node
    {
        private Node val;

        public UMinusNode(Node val) { this.val = val; }

        public override object evaluate(Context ctx)
        {
            object o = val.evaluate(ctx);

            if (o is double d)
                return -d;

            return new RAWNull();
        }
    }

    class NewNode : Node
    {
        private Node val;

        public NewNode(Node val) { this.val = val; }

        public override object evaluate(Context ctx)
        {
            return HelperMethods.CopyObject(val.evaluate(ctx));
        }
    }

    class GetterSetter
    {
        public RAWFunction getter;
        public RAWFunction setter;

        public GetterSetter(RAWFunction getter, RAWFunction setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        public object Get(object owner, Context ctx)
        {
            getter.self_reference = owner;
            return getter.Run(ctx, new List<object>());
        }

        public void Set(object owner, Context ctx, object value)
        {
            setter.self_reference = owner;
            setter.Run(ctx, new List<object> { value });
        }
    }

}