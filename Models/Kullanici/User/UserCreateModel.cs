using System.ComponentModel.DataAnnotations;

namespace UniCP.Models.Kullanici.User;

public class UserCreateModel
{
    [Required]
    [Display(Name = "Ad Soyad")]
    public string AdSoyad { get; set; } = null!;

    [Required]
    [Display(Name = "Eposta")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Şifre alanı zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = null!;
    public int? LNGORTAKFIRMAKOD { get; set; }
    public int? LNGKULLANICITIPI { get; set; }
    public List<int>? SelectedCompanyIds { get; set; } = new List<int>();
}