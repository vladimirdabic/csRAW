using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAW
{
    public class ParserError : Exception {
        public ParserError(string message) : base(message) { }
        public ParserError(string message, Exception inner) : base(message, inner) { }
    };
    class Parser
    {
        private List<Token> tokens;
        private int current = 0;

        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
            current = 0;
        }

        public Node parse()
        {
            List<Node> statements = new List<Node>();

            while(!isAtEnd())
            {
                statements.Add(ParseDeclaration());
            }

            return new MainContainer(new BlockNode(statements), true);
        }

        public Node ParseDeclaration()
        {
            if (match(TokenType.FUN)) return ParseFuncDef();
            return ParseStatement();
        }

        public FuncDefNode ParseFuncDef()
        {
            Token func_name = consume(TokenType.IDENTIFIER, "Expected function name after 'func' keyword");
            consume(TokenType.LEFT_PAREN, "Expected '(' after function name");


            List<string> args = new List<string>();

            if(!check(TokenType.RIGHT_PAREN))
            {
                do
                {
                    if (!check(TokenType.IDENTIFIER))
                        throw error(peek(), "Expected identifier in function parameter definition");
                    args.Add(advance().lexeme);
                } while (match(TokenType.COMMA));
            }

            consume(TokenType.RIGHT_PAREN, "Expected ')' after function parameter definition");

            BlockNode code;
            if (match(TokenType.LEFT_BRACE)) code = GetBlock();
            else throw error(peek(), "Expected '{' after function declaration");

            return new FuncDefNode(func_name, args, code);
        }

        public Node ParseStatement(bool ctx_container = true)
        {
            if (match(TokenType.IF)) return ParseIFStmt();
            if (match(TokenType.WHILE)) return ParseWhileStmt();
            if (match(TokenType.GLOBAL)) return ParseGlobal();
            if (match(TokenType.RETURN)) return ParseReturn();
            if (match(TokenType.PASS)) return ParsePass();
            if (match(TokenType.FOR)) return ParseFor();
            if (match(TokenType.FOREACH)) return ParseForEach();
            if (match(TokenType.LEFT_BRACE))
            {
                if (ctx_container) return new CTXContainer(GetBlock());
                return GetBlock();
            };
            return ParseExprStatement();
        }

        public Node ParseFor()
        {
            consume(TokenType.LEFT_PAREN, "Expected '(' after for");

            if (match(TokenType.RIGHT_PAREN) || isAtEnd())
                throw error(previous(), "Expected expression for the for statement");

            Token varname = consume(TokenType.IDENTIFIER, "Expected variable name in for loop");
            consume(TokenType.COMMA, "Expected ',' after variable name in for loop");

            Node start = ParseExpression();
            consume(TokenType.COMMA, "Expected ',' after starting value in for loop");

            Node end = ParseExpression();
            consume(TokenType.RIGHT_PAREN, "Expected ')' after end value in for loop");

            Node statement = ParseStatement(false);

            return new FORNode(varname, start, end ,statement);
        }

        public Node ParseForEach()
        {
            consume(TokenType.LEFT_PAREN, "Expected '(' after foreach");

            if (match(TokenType.RIGHT_PAREN) || isAtEnd())
                throw error(previous(), "Expected expression for the foreach statement");

            Token varname = consume(TokenType.IDENTIFIER, "Expected variable name in foreach loop");
            consume(TokenType.COLON, "Expected ':' after variable name in foreach loop");

            Node loop_obj = ParseExpression();
            consume(TokenType.RIGHT_PAREN, "Expected ')' after value in foreach loop");

            Node statement = ParseStatement(false);

            return new FOREachNode(varname, loop_obj, statement);
        }

        public Node ParseIFStmt()
        {
            consume(TokenType.LEFT_PAREN, "Expected '(' after if");

            if (match(TokenType.RIGHT_PAREN) || isAtEnd())
                throw error(previous(), "Expected expression for the if statement");

            Node expr = ParseExpression();
            consume(TokenType.RIGHT_PAREN, "Expected ')' after if expression");

            Node statement = ParseStatement(false);

            return new IFNode(expr, statement);
        }

        public Node ParseWhileStmt()
        {
            consume(TokenType.LEFT_PAREN, "Expected '(' after while");

            if (match(TokenType.RIGHT_PAREN) || isAtEnd())
                throw error(previous(), "Expected expression for the while statement");

            Node expr = ParseExpression();
            consume(TokenType.RIGHT_PAREN, "Expected ')' after while expression");

            Node statement = ParseStatement(false);

            return new WhileNode(expr, statement);
        }

        public GlobalDeclNode ParseGlobal()
        {
            Token var = consume(TokenType.IDENTIFIER, "Expected variable name after 'global'");
            consume(TokenType.SEMICOLON, "Expected ';' after global declaration statement");
            return new GlobalDeclNode(var);
        }

        public Node ParsePass()
        {
            Token varname = consume(TokenType.IDENTIFIER, "Expected variable name after 'pass'");
            consume(TokenType.LEFT_BRACE, "Expected '{' for the pass statement");

            MainContainer block = new MainContainer(GetBlock());

            consume(TokenType.SEMICOLON, "Expected ';' after pass statement");

            return new PassNode(varname, block);
        }

        public Node ParseReturn()
        {
            Node expr = null;

            if (!check(TokenType.SEMICOLON))
                expr = ParseExpression();

            consume(TokenType.SEMICOLON, "Expected ';' after return value");
            return new ReturnNode(expr);
        }

        public BlockNode GetBlock()
        {
            List<Node> statements = new List<Node>();

            while(!check(TokenType.RIGHT_BRACE) && !isAtEnd())
            {
                statements.Add(ParseDeclaration());
            }

            consume(TokenType.RIGHT_BRACE, "Expected '}' at the end of a scope block.");
            return new BlockNode(statements);
        }

        public Node ParseExprStatement()
        {
            Node expr = ParseExpression();
            consume(TokenType.SEMICOLON, "Expected ';' after expression statement");
            return expr;
        }

        public Node ParseExpression()
        {
            return ParseAssignment();
        }

        public Node ParseAssignment()
        {
            Node left = ParseOr();

            if(match(TokenType.EQUAL))
            {
                Node right = ParseAssignment();

                if(left is VariableNode)
                {
                    Token name = ((VariableNode)left).variable;
                    return new AssignNode(name, right, ((VariableNode)left).is_global);
                }
                else if (left is TableGetNode)
                {
                    TableGetNode get = (TableGetNode)left;
                    return new TableSetNode(get.value, get.get_name, right);
                }
                else if (left is TableGetExprNode)
                {
                    TableGetExprNode get = (TableGetExprNode)left;
                    return new TableSetExprNode(get.value, get.get_expr, right);
                }
            }

            return left;
        }

        public Node ParseOr()
        {
            Node left = ParseAnd();

            while (match(TokenType.OR))
            {
                Token op = previous();
                Node right = ParseAnd();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseAnd()
        {
            Node left = ParseEquals();

            while (match(TokenType.AND))
            {
                Token op = previous();
                Node right = ParseEquals();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseEquals()
        {
            Node left = ParseComparison();

            while (match(TokenType.EQUAL_EQUAL, TokenType.BANG_EQUAL))
            {
                Token op = previous();
                Node right = ParseComparison();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseComparison()
        {
            Node left = ParseTerm();

            while (match(TokenType.GREATER, TokenType.GREATER_EQUAL, TokenType.LESS, TokenType.LESS_EQUAL))
            {
                Token op = previous();
                Node right = ParseTerm();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseTerm()
        {
            Node left = ParseFactor();

            while (match(TokenType.PLUS, TokenType.MINUS))
            {
                Token op = previous();
                Node right = ParseFactor();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseFactor()
        {
            Node left = ParseGetAndCall();

            while(match(TokenType.STAR, TokenType.SLASH))
            {
                Token op = previous();
                Node right = ParseGetAndCall();
                left = new BinaryOpNode(left, right, op);
            }

            return left;
        }

        public Node ParseGetAndCall()
        {
            Node left = ParsePrimary();

            while(true)
            {
                if(match(TokenType.LEFT_PAREN))
                {
                    left = FinishParseCall(left);
                }
                else if(match(TokenType.DOT))
                {
                    Token name = consume(TokenType.IDENTIFIER, "Expected property name after '.'");
                    left = new TableGetNode(left, name);
                }
                else if (match(TokenType.POINTER_ARROW))
                {
                    Token name = consume(TokenType.IDENTIFIER, "Expected function name after '->'");
                    left = new TableGetNode(left, name, true);
                }
                else if(match(TokenType.LEFT_SQR))
                {

                    if (match(TokenType.RIGHT_SQR))
                        throw error(previous(), "Expected expression inside indexing");

                    Node index_expr = ParseExpression();
                    consume(TokenType.RIGHT_SQR, "Expected ']' after indexing expression");

                    left = new TableGetExprNode(left, index_expr); 
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        public Node FinishParseCall(Node value_to_call)
        {
            List<Node> arguments = new List<Node>();
            if(!check(TokenType.RIGHT_PAREN))
            {
                do
                {
                    arguments.Add(ParseExpression());
                } while (match(TokenType.COMMA));
            }

            consume(TokenType.RIGHT_PAREN, "Expected ')' after function arguments");
            return new CallFunctionNode(value_to_call, arguments);
        }

        public Node ParsePrimary()
        {

            if (match(TokenType.TRUE)) return new LiteralNode(true);
            if (match(TokenType.FALSE)) return new LiteralNode(false);
            if (match(TokenType.NIL)) return new LiteralNode(new RAWNull());

            if (match(TokenType.NUMBER, TokenType.STRING))
                return new LiteralNode(previous().literal);

            if (match(TokenType.IDENTIFIER))
            {
                Token vartkn = previous();

                if (match(TokenType.PLUS_PLUS))
                    return new VarIncDec(vartkn, false, false);

                if (match(TokenType.MINUS_MINUS))
                    return new VarIncDec(vartkn, true, false);

                return new VariableNode(vartkn, false);
            }

            if (match(TokenType.DOLLAR))
            {
                Token vartkn = consume(TokenType.IDENTIFIER, "Expected variable after '$'");
                return new VariableNode(vartkn, true);
            }

            if (match(TokenType.PLUS_PLUS))
            {
                Token varname = consume(TokenType.IDENTIFIER, "Expected variable name after '++'");
                return new VarIncDec(varname, false, true);
            }

            if (match(TokenType.MINUS_MINUS))
            {
                Token varname = consume(TokenType.IDENTIFIER, "Expected variable name after '--'");
                return new VarIncDec(varname, true, true);
            }

            if (match(TokenType.LEFT_PAREN))
            {
                Node expr = ParseExpression();
                consume(TokenType.RIGHT_PAREN, "Expected ')' after expression.");
                return expr;
            }

            if(match(TokenType.LEFT_BRACE))
                return new TableNode(ParseTablePairs());

            if (match(TokenType.FUN))
                return new LiteralNode(ParseFuncValue());

            if (match(TokenType.LEFT_SQR))
                return ParseArray();

            if (match(TokenType.BANG))
            {
                Node expr = ParseExpression();
                return new NOTNode(expr);
            }

            if (match(TokenType.MINUS))
            {
                Node expr = ParsePrimary();
                return new UMinusNode(expr);
            }

            if(match(TokenType.NEW))
            {
                Node expr = ParsePrimary();
                return new NewNode(expr);
            }

            throw error(peek(), "Expected expression");
        }

        public RAWFunction ParseFuncValue()
        {
            consume(TokenType.LEFT_PAREN, "Expected '(' after function value keyword");

            List<string> args = new List<string>();

            if (!check(TokenType.RIGHT_PAREN))
            {
                do
                {
                    if (!check(TokenType.IDENTIFIER))
                        throw error(peek(), "Expected identifier in function parameter definition");

                    args.Add(advance().lexeme);
                } while (match(TokenType.COMMA));
            }

            consume(TokenType.RIGHT_PAREN, "Expected ')' after function parameter definition");

            BlockNode code;
            if (match(TokenType.LEFT_BRACE)) code = GetBlock();
            else throw error(peek(), "Expected '{' after function declaration");

            return new RAWFunction(code, args);
        }

        public List<TableKVNode> ParseTablePairs()
        {
            List<TableKVNode> kvPairs = new List<TableKVNode>();

            if (!check(TokenType.RIGHT_BRACE))
            {
                do
                {

                    Node key = ParseExpression();
                    consume(TokenType.COLON, "Expected ':' after table key");
                    Node value = ParseExpression();

                    kvPairs.Add(new TableKVNode(key, value));

                } while (match(TokenType.COMMA));
            }

            consume(TokenType.RIGHT_BRACE, "Expected '}' after table values");
            return kvPairs;
        }

        public ArrayNode ParseArray()
        {
            List<Node> elements = new List<Node>();

            if(!check(TokenType.RIGHT_SQR))
            {
                do
                {
                    elements.Add(ParseExpression());
                } while (match(TokenType.COMMA));
            }

            consume(TokenType.RIGHT_SQR, "Expected ']' after array values");

            return new ArrayNode(elements);
        }

        private bool match(params TokenType[] types)
        {
            foreach (TokenType type in types)
            {
                if (check(type))
                {
                    advance();
                    return true;
                }
            }

            return false;
        }

        private Token consume(TokenType type, String message)
        {
            if (check(type)) return advance();

            throw error(peek(), message);
        }

        private bool check(TokenType type)
        {
            if (isAtEnd()) return false;
            return peek().type == type;
        }

        private Token advance()
        {
            if (!isAtEnd()) current++;
            return previous();
        }

        private bool isAtEnd()
        {
            return peek().type == TokenType.EOF;
        }

        private Token peek()
        {
            return tokens[current];
        }

        private Token previous()
        {
            return tokens[current - 1];
        }

        private ParserError error(Token token, string message)
        {
            return new ParserError($"[Line {token.line}] {message}");
        }
    }
}
