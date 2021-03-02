using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RAW
{
    public class ScannerError : Exception
    {
        public ScannerError(string message) : base(message) { }
        public ScannerError(string message, Exception inner) : base(message, inner) { }
    };
    class Scanner
    {
        private int start = 0;
        private int current = 0;
        private int line = 1;
        private string source;
        private List<Token> tokens = new List<Token> { };

        public static Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>
        {
            { "and", TokenType.AND },
            { "var", TokenType.VAR },
            { "while", TokenType.WHILE },
            { "func", TokenType.FUN },
            { "for", TokenType.FOR },
            { "foreach", TokenType.FOREACH },
            { "if", TokenType.IF },
            { "nil", TokenType.NIL },
            { "null", TokenType.NIL },
            { "return", TokenType.RETURN },
            { "else", TokenType.ELSE },
            { "or", TokenType.OR },
            { "true", TokenType.TRUE },
            { "false", TokenType.FALSE },
            { "pass", TokenType.PASS },
            { "potato", TokenType.PASS },
            { "global", TokenType.GLOBAL }
        };

        public Scanner(string source)
        {
            this.source = source;
        }

        public List<Token> scanTokens()
        {
            
            while(!isAtEnd())
            {
                start = current;
                scanToken();
            }

            tokens.Add(new Token(TokenType.EOF, "", null, line));

            return tokens;
        }


        private void scanToken()
        {
            char c = advance();

            switch(c)
            {
                case '(': addToken(TokenType.LEFT_PAREN); break;
                case ')': addToken(TokenType.RIGHT_PAREN); break;
                case '{': addToken(TokenType.LEFT_BRACE); break;
                case '}': addToken(TokenType.RIGHT_BRACE); break;
                case '[': addToken(TokenType.LEFT_SQR); break;
                case ']': addToken(TokenType.RIGHT_SQR); break;
                case ',': addToken(TokenType.COMMA); break;
                case '.': addToken(TokenType.DOT); break;
                case '-':
                    addToken(match('>') ? TokenType.POINTER_ARROW : match('-') ? TokenType.MINUS_MINUS : TokenType.MINUS);
                    break;
                case '+':
                    addToken(match('+') ? TokenType.PLUS_PLUS : TokenType.PLUS);
                    break;
                case ';': addToken(TokenType.SEMICOLON); break;
                case '*': addToken(TokenType.STAR); break;
                case ':': addToken(TokenType.COLON); break;

                case '!':
                    addToken(match('=') ? TokenType.BANG_EQUAL : TokenType.BANG);
                    break;
                case '=':
                    addToken(match('=') ? TokenType.EQUAL_EQUAL : TokenType.EQUAL);
                    break;
                case '>':
                    addToken(match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER);
                    break;
                case '<':
                    addToken(match('=') ? TokenType.LESS_EQUAL : TokenType.LESS);
                    break;

                case '/':
                    if(match('/'))
                    {
                        // Skip the whole line
                        while (peek() != '\n' && !isAtEnd()) advance(); 
                    }
                    else
                    {
                        addToken(TokenType.SLASH);
                    }
                    break;

                case ' ':
                case '\r':
                case '\t':
                    break;

                case '\n':
                    line++;
                    break;

                // string
                case '"': readString(); break;

                default:

                    if(isDigit(c))
                    {
                        readNumber();
                    }
                    else if (isAlpha(c))
                    {
                        readIdentifier();
                    }
                    else
                    {
                        throw error("Unexpected character '" + c + "'");
                    }
                    break;
            }
        }

        // Readers
        private void readString()
        {
            while(peek() != '"' && !isAtEnd())
            {
                if (peek() == '\n') line++;
                advance();
            }

            if (isAtEnd())
            {
                throw error("Unterminated string at EOF");
            }

            // Consume the closing "
            advance();

            string val = source.Substring(start+1, current - start - 2);
            addToken(TokenType.STRING, val);
        }
        
        private void readNumber()
        {
            // Keep going until we aren't at a digit anymore
            while(isDigit(peek())) advance();

            // Look for dot
            if(peek() == '.')
            {
                // Consume dot and read rest of the number
                advance();
                while (isDigit(peek())) advance();
            }

            double val = double.Parse(source.Substring(start, current - start));
            addToken(TokenType.NUMBER, val);
        }

        private void readIdentifier()
        {
            while (isAlphaNumeric(peek())) advance();

            string lexeme = source.Substring(start, current - start);
            TokenType type = keywords.ContainsKey(lexeme) ? keywords[lexeme] : TokenType.IDENTIFIER;
            addToken(type);
        }

        // Helper methods

        private bool isAtEnd()
        {
            return current >= source.Length;
        }

        private char advance()
        {
            return source[current++];
        }

        private char peek()
        {
            if (isAtEnd()) return '\0';
            return source[current];
        }

        private char peekNext()
        {
            if (current + 1 >= source.Length) return '\0';
            return source[current + 1];
        }

        private bool match(char expected)
        {
            if (isAtEnd()) return false;
            if (source[current] != expected) return false;

            current++;
            return true;
        }

        // Check if the character is a valid character
        private bool isAlpha(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        private bool isDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private bool isAlphaNumeric(char c)
        {
            return isAlpha(c) || isDigit(c);
        }

        private void addToken(TokenType type, object literal = null)
        {
            string lexeme = source.Substring(start, current - start);
            tokens.Add(new Token(type, lexeme, literal, line));
        }

        private ScannerError error(string message)
        {
            return new ScannerError($"[Line {line}] {message}");
        }
    }
}
