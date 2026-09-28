using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Api.Controllers;

// ---------------------------------------------------------------------------------
// CHANGED IN 4.3: this class is now EMPTY on purpose.
// ToProblem(...) and NotFoundProblem(...) were DELETED, because building error
// responses is no longer a controller's job: controllers THROW, and
// RondiTrackExceptionHandler builds the response. Two systems must not run side by side.
// (You may delete this file and inherit ControllerBase directly instead.)
// ---------------------------------------------------------------------------------
public abstract class ApiControllerBase : ControllerBase
{
}
