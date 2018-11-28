using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.GithubIssues
{
    // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/24

    public class CreateImageRequest
    {
        public IFormFile File { get; set; }
        public string Name { get; set; }
    }

    public class SampleRequest
    {
        public int Offset { get; set; }

        public int Count { get; set; }
    }

    public static class ErrorCode
    {
        public static string MustNotBeEmpty = "MustNotBeEmpty";
        public static string ExpectationNotMet = "ExpectationNotMet";
        public static string IncorrectLength = "IncorrectLength";
    }

    public class CreateImageRequestValidator : AbstractValidator<CreateImageRequest>
    {
        public CreateImageRequestValidator()
        {
            RuleFor(_ => _.File).NotEmpty().WithErrorCode(ErrorCode.MustNotBeEmpty.ToString());
            RuleFor(_ => _.File).Must((request, ct) =>
            {
                if ((request?.File?.ContentType == @"image/jpeg") || (request?.File?.ContentType == @"image/png"))
                {
                    return true;
                }
                return false;
            }).WithMessage("Invalid content type").WithErrorCode(ErrorCode.ExpectationNotMet.ToString());
            RuleFor(_ => _.Name).Length(0, 250).WithErrorCode(ErrorCode.IncorrectLength.ToString());
        }
    }

    [Route("api/[controller]")]
    public class ImageController : Controller
    {
        [HttpPost("[action]")]
        public async Task<IActionResult> CreateProfileImage([FromForm]CreateImageRequest request)
        {
            return Ok();
        }

        [HttpPut]
        public Task Put([FromForm] SampleRequest request)
        {
            return Task.CompletedTask;
        }
    }
}
