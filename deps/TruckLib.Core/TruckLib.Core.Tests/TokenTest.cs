using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TruckLib;

namespace TruckLib.Core.Tests
{
    public class TokenTest
    {
        [Fact]
        public void StringToToken()
        {
            Assert.Equal((Token)0x1573C3E700, Token.StringToToken("default"));
        }

        [Fact]
        public void EmptyStringToToken()
        {
            Assert.Equal((Token)0, Token.StringToToken(""));
        }

        [Fact]
        public void StringToTokenThrowsOnInvalid()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Token.StringToToken("A");
            });
        }

        [Fact]
        public void TokenToString()
        {
            Assert.Equal("default", Token.TokenToString(0x1573C3E700));
        }

        [Fact]
        public void EmptyTokenToString()
        {
            Assert.Equal("", Token.TokenToString(0));
        }

        [Theory]
        [InlineData("abc_def", true)]
        [InlineData("aaaaaaaaaaaaa", false)]
        [InlineData("A", false)]
        public void IsValidToken(string input, bool valid)
        {
            Assert.Equal(valid, Token.IsValidToken(input));
        }

        [Fact]
        public void SetString()
        {
            var token = new Token();
            token.String = "default";
            Assert.Equal(0x1573C3E700UL, token.Value);
        }

        [Fact]
        public void ToString_()
        {
            var token = new Token("default");
            Assert.Equal("default", token.ToString());
        }

        [Fact]
        public void ImplicitCastThrowsOnNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var a = (Token)(-42);
            });
        }
    }
}
