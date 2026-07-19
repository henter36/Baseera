using Baseera.Api.Endpoints;
using Baseera.Domain.Notes;

namespace Baseera.UnitTests;

public sealed class NoteListQueryParamsTests
{
    [Fact]
    public void ToQuery_applies_contract_defaults_when_properties_unbound()
    {
        var query = new NoteListQueryParams().ToQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.False(query.OverdueOnly);
        Assert.False(query.SortDesc);
        Assert.Null(query.Status);
        Assert.Null(query.FacilityId);
    }

    [Fact]
    public void ToQuery_preserves_explicit_filter_values()
    {
        var facilityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var query = new NoteListQueryParams
        {
            Page = 2,
            PageSize = 10,
            Status = NoteStatus.Open,
            Severity = NoteSeverity.High,
            FacilityId = facilityId,
            OverdueOnly = true,
            SortBy = "severity",
            SortDesc = true
        }.ToQuery();

        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);
        Assert.Equal(NoteStatus.Open, query.Status);
        Assert.Equal(NoteSeverity.High, query.Severity);
        Assert.Equal(facilityId, query.FacilityId);
        Assert.True(query.OverdueOnly);
        Assert.Equal("severity", query.SortBy);
        Assert.True(query.SortDesc);
    }
}
