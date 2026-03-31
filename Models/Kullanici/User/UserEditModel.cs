using System.ComponentModel.DataAnnotations;

namespace SOS.Models.Kullanici.User;

public class UserEditModel
{
    [Required]
    [Display(Name = "Ad Soyad")]
    public string AdSoyad { get; set; } = null!;

    [Required]
    [Display(Name = "Eposta")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Display(Name = "Parola")]
    [DataType(DataType.Password)]
    public string? Password { get; set; } = null!;

    [Display(Name = "Parola Tekrar")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Parola eşleşmiyor")]
    public string? ConfirmPassword { get; set; } = null!;
    public IList<string>? SelectedRoles { get; set; }
    public int? LNGORTAKFIRMAKOD { get; set; }
    public int? LNGKULLANICITIPI { get; set; }
    public List<int>? SelectedCompanyIds { get; set; } = new List<int>();
}
