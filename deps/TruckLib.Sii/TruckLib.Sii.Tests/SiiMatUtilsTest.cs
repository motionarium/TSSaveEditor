using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii.Tests
{
    public class SiiMatUtilsTest
    {
        [Fact]
        public void RemoveSingleLineComments()
        {
            var input = """
                hello / there
                bla //remove this
                foo
                # and this
                bar /
                """;
            var expected = """
                hello / there
                bla 
                foo
                
                bar /
                """;
            Assert.Equal(expected, SiiMatUtils.RemoveComments(input));
        }

        [Fact]
        public void RemoveBlockComments()
        {
            var input = """
                hello / there
                bla /*remove this
                foo
                # and this*/
                bar
                """;
            var expected = """
                hello / there
                bla 
                bar
                """;
            Assert.Equal(expected, SiiMatUtils.RemoveComments(input));
        }

        [Fact]
        public void RemoveCommentsAndIgnoreQuotedText()
        {
            var input = """
                hello //remove this
                but "not #this /* asdf */"
                """;
            var expected = """
                hello 
                but "not #this /* asdf */"
                """;
            Assert.Equal(expected, SiiMatUtils.RemoveComments(input));
        }
    }
}
