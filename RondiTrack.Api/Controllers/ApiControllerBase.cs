using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ToProblem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        return Problem(title: error.Code, detail: error.Message, statusCode: status);
    }
    //New: for every place that used to just say NotFound() with an empty body.
      protected ObjectResult NotFoundProblem(string code, string detail) =>
        Problem(title: code, detail: detail, statusCode: StatusCodes.Status404NotFound);
}

