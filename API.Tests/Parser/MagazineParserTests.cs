using Xunit;

namespace API.Tests.Parser
{
    public class MagazineParserTests
    {
        [Theory]
        [InlineData("Imagine FX - 2012 01", "Imagine FX")]
        public void ParseMagazineSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseMagazineSeries(filename));
        }

        [Theory]
        [InlineData("Imagine FX - 2012 01", "1")]
        public void ParseMagazineChapterTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseMagazineChapter(filename));
        }
    }
}
