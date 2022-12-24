namespace Protsyk.PMS.FullText.Core.UnitTests;

public class ParserTest
{
    [Theory]
    [InlineData("WORD(petro)", "WORD(petro)")]
    [InlineData("EDIT(sophie  , 2)", "EDIT(sophie,2)")]
    [InlineData("WILD(mariya*)", "WILD(mariya*)")]
    [InlineData("OR(WORD(petro), WORD(sophie), WORD(mariya))", "OR(WORD(petro),WORD(sophie),WORD(mariya))")]
    [InlineData("SEQ(WORD(PMS), WORD(petro), WORD(sophie), WORD(mariya))", "SEQ(WORD(PMS),WORD(petro),WORD(sophie),WORD(mariya))")]
    [InlineData("OR(AND(WORD(apple),WORD(ap\\*ple), WILD(ap?le*),EDIT(appl,1)), WORD(ba\\)nana\\~1))", "OR(AND(WORD(apple),WORD(ap\\*ple),WILD(ap?le*),EDIT(appl,1)),WORD(ba\\)nana\\~1))")]
    public void TestQueryParserForInput(string input, string expected)
    {
        var parser = new QueryParser();
        var query = parser.Parse(input);

        Assert.NotNull(query);
        Assert.Equal(expected, query.ToString());
    }

    [Theory]
    [InlineData("WORD(petro) and apple", 11)]
    public void TestQueryParserForWrongInput(string input, int position)
    {
        bool error = false;
        try
        {
            new QueryParser().Parse(input);
        }
        catch(QueryParserException e)
        {
            error = true;
            Assert.Equal(position, e.Position);
        }
        Assert.True(error);
    }
}
