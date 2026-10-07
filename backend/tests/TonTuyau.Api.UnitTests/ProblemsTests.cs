using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.UnitTests;

public sealed class ProblemsTests
{
    [Fact]
    public void Validation_problems_list_every_message_by_parameter()
    {
        ValidationException error = new(
            "request",
            [
                new("sort", "must be one of latest, popular, oldest."),
                new("size", "must be between 1 and 100."),
                new("sort", "again."),
            ]);

        HttpValidationProblemDetails problem = Assert.IsType<HttpValidationProblemDetails>(Problems.Validation(error));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("https://tontuyau/errors/validation", problem.Type);
        Assert.Equal("Invalid request", problem.Title);
        Assert.Equal(["must be one of latest, popular, oldest.", "again."], problem.Errors["sort"]);
        Assert.Equal(["must be between 1 and 100."], problem.Errors["size"]);
        Assert.Equal(
            "sort: must be one of latest, popular, oldest. size: must be between 1 and 100. sort: again.",
            problem.Detail);
    }

    [Fact]
    public void An_unexpected_error_says_nothing_about_itself()
    {
        ProblemDetails problem = Problems.Unexpected();

        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("https://tontuyau/errors/internal", problem.Type);
        Assert.DoesNotContain("Exception", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(404, "https://tontuyau/errors/not-found")]
    [InlineData(405, "https://tontuyau/errors/method-not-allowed")]
    [InlineData(415, "https://tontuyau/errors/http-415")]
    public void Bodiless_errors_get_a_problem_too(int status, string type)
    {
        ProblemDetails problem = Problems.ForStatus(status);

        Assert.Equal((status, type), (problem.Status, problem.Type));
        Assert.False(string.IsNullOrEmpty(problem.Title));
    }

    [Fact]
    public void A_bad_range_names_the_file_size()
    {
        ProblemDetails problem = Problems.RangeNotSatisfiable(1000);

        Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, problem.Status);
        Assert.Contains("1000", problem.Detail, StringComparison.Ordinal);
    }
}
