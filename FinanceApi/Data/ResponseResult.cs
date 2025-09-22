namespace FinanceApi.Data
{
    public class ResponseResult<T>
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public ResponseResult(bool status, string message, T data)
        {
            Status = status;
            Message = message;
            Data = data;
        }
    }

}
