namespace YetkiliServisGazAcma.Business.Services;

public class ApiIslemSonuc
{
    public bool Basarili { get; set; }
    public string? Mesaj { get; set; }
    public int? Id { get; set; }

    public static ApiIslemSonuc BasariliSonuc(string mesaj) => new() { Basarili = true, Mesaj = mesaj };
    public static ApiIslemSonuc Basarisiz(string mesaj) => new() { Basarili = false, Mesaj = mesaj };
}
