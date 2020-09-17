namespace csharp_api.Transfer.Response.Token
{
    public abstract class Default
    {
        public string token { get; set; }
        public string token_type { get; set; }
    }

    public class Registration : Default
    {
        public Registration(string token)
        {
            this.token = token;
            this.token_type = "registration";
        }
    }

    public class Refresh : Default
    {
        public Refresh(string token)
        {
            this.token = token;
            this.token_type = "refresh";
        }
    }

    public class Access : Default
    {
        public Access(string token)
        {
            this.token = token;
            this.token_type = "access";
        }
    }
}