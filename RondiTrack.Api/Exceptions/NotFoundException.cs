namespace RondiTrack.Api.Exceptions;

     // The resource the URL itself is ABOUT does not exist. -> 404
        public class NotFoundException(string code, string message) : RondiTrackException(code, message);
     
