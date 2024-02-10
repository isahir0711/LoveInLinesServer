namespace LoveInLinesServer.DTOs
{
    public class ClientMessage
    {
        public string ColorCode { get; set; }
        public int LineWidth { get; set; }
        public float XPosition { get; set; }
        public float YPosition { get; set; }

        public string Message { get; set; }

        public string Room { get; set; }
    }
}
