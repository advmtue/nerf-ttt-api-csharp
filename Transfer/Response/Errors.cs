namespace csharp_api.Transfer.Response.Error
{
    public class APIError
    {
        public string Message { get; }
        public string Code { get; }

        public APIError(string message, string code)
        {
            this.Message = message;
            this.Code = code;
        }
    }
}