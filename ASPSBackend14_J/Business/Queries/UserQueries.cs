using Common.Entities;
using Common.Messaging;
using Common.Models;

namespace Business.Queries;

public class GetAllUsersQuery : Query { }

public class GetAllUsersQueryResult : QueryResult
{
    public List<User> Users { get; set; } = new();
}

public class GetUserByKeyQuery : Query
{
    public GetUserByKeyQuery()
    {
        QueryType = nameof(GetUserByKeyQuery);
    }
    
    public string QueryType { get; set; } = string.Empty;
    public Key UserKey { get; set; } = new Key();
}

public class GetUserByKeyQueryResult : QueryResult
{
    public User? User { get; set; }
}

public class GetUserDetailsQuery : Query
{
    public Key UserKey { get; set; } = new Key();
}

public class GetUserDetailsQueryResult : QueryResult
{
    public User? User { get; set; }
}

public class GetUserDevicesQuery : Query
{
    public Key UserKey { get; set; } = new Key();
}

public class GetUserDevicesQueryResult : QueryResult
{
    public List<UserDevice> Devices { get; set; } = new();
}

public class GetUserAccountsQuery : Query
{
    public Key UserKey { get; set; } = new Key();
}

public class GetUserAccountsQueryResult : QueryResult
{
    public List<UserAccount> Accounts { get; set; } = new();
}

public class GetUserByKeycloakIdQuery : Query
{
    public GetUserByKeycloakIdQuery()
    {
        QueryType = nameof(GetUserByKeycloakIdQuery);
    }
    
    public string QueryType { get; set; } = string.Empty;
    public string KeycloakUserId { get; set; } = string.Empty;
}

public class GetUserByKeycloakIdQueryResult : QueryResult
{
    public User? User { get; set; }
}
