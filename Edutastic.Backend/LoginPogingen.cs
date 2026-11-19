public class LoginPogingen
{
    public int Aantal { get; set; } = 0;

    public DateTimeOffset GeblokkeerdTot { get; set; }
        = DateTimeOffset.MinValue;
    
    public SemaphoreSlim Slot { get; } = new(1, 1);
}