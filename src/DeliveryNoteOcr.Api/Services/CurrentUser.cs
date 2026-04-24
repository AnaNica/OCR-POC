namespace DeliveryNoteOcr.Api.Services;

public interface ICurrentUser
{
    string UserId { get; }
}

public class DevCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public DevCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string UserId
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? "dev-user" : header;
        }
    }
}
