using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public VariableNode(Token variable) { this.variable = variable; }
        public override object evaluate(Context ctx)
        {
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

            ((RAWTable)val)[set_name.lexeme] = value.evaluate(ctx);

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

            if (!(val is RAWTable))
                throw new RuntimeError("Tried assinging a property to a non table value");

            ((RAWTable)val)[set_name.evaluate(ctx)] = value.evaluate(ctx);

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

                if (value is RAWFunction && pass_self)
                    ((RAWFunction)value).self_reference = val;

                return value;
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
            if (val is RAWTable) return ((RAWTable)val)[get_expr.evaluate(ctx)];
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

        public AssignNode(Token varname, Node expr)
        {
            this.varname = varname;
            this.expr = expr;

        }

        public override object evaluate(Context ctx)
        {
            object val = expr.evaluate(ctx);
            ctx.SetVar(varname.lexeme, val);
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
        private Node statement, check, expr, decl;

        public FORNode(Node decl, Node check, Node expr, Node statement)
        {
            this.expr = expr;
            this.statement = statement;
            this.decl = decl;
            this.check = check;
        }

        public override object evaluate(Context ctx)
        {
            if (decl != null)
                decl.evaluate(ctx);

            do
            {
                object result = check.evaluate(ctx);

                if (result is RAWNull) return null;
                if (result is bool && !(bool)result) return null;

                statement.evaluate(ctx);

                if (expr != null)
                    expr.evaluate(ctx);

            } while (true);
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

}