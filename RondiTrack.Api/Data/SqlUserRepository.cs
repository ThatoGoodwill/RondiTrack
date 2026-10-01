using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public sealed class SqlUserRepository(RondiTrackDbContext context) : IUserRepository
{

}