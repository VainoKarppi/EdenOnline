namespace EdenOnline.Models;


public class ArmaClient
{
    public int Id { get; set; } = -1;
    public string Username { get; set; } = "";
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;
    public double Z { get; set; } = 0;

    public double Direction { get; set; } = 0;
    public double Pitch { get; set; } = 0;
}