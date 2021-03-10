using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAW
{

    enum TokenType
    {
        // Single-character tokens.
        LEFT_PAREN, RIGHT_PAREN, LEFT_BRACE, RIGHT_BRACE,
        COMMA, DOT, MINUS, PLUS, SEMICOLON, SLASH, STAR, COLON,
        LEFT_SQR, RIGHT_SQR, DOLLAR,

        // One or two character tokens.
        BANG, BANG_EQUAL,
        EQUAL, EQUAL_EQUAL,
        GREATER, GREATER_EQUAL,
        LESS, LESS_EQUAL, POINTER_ARROW, PLUS_PLUS, MINUS_MINUS,

        // Literals.
        IDENTIFIER, STRING, NUMBER,

        // Keywords.
        AND, CLASS, ELSE, FALSE, FUN, FOR, FOREACH, IF, NIL, OR,
        PRINT, RETURN, SUPER, THIS, TRUE, VAR, WHILE,
        PASS, GLOBAL, NEW,

        EOF
    }

    class Token
    {
        public TokenType type;
        public string lexeme;
        public object literal;
        public int line;

        public Token(TokenType type, string lexeme, object literal, int line)
        {
            this.type = type;
            this.lexeme = lexeme;
            this.literal = literal;
            this.line = line;
        }

        public override string ToString()
        {
            return literal != null ? $"<Token type={type}, lexeme='{lexeme}', literal={literal}>" : $"<Token type={type}, lexeme='{lexeme}'>";
        }

    }
}
