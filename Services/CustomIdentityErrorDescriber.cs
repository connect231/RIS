using Microsoft.AspNetCore.Identity;

namespace SOS.Services
{
    public class CustomIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DefaultError()
        {
            return new IdentityError { Code = nameof(DefaultError), Description = $"Bilinmeyen bir hata oluştu." };
        }

        public override IdentityError ConcurrencyFailure()
        {
            return new IdentityError { Code = nameof(ConcurrencyFailure), Description = "İyimser eşzamanlılık hatası, nesne değiştirilmiş." };
        }

        public override IdentityError PasswordMismatch()
        {
            return new IdentityError { Code = nameof(PasswordMismatch), Description = "Parola uyuşmuyor." };
        }

        public override IdentityError InvalidToken()
        {
            return new IdentityError { Code = nameof(InvalidToken), Description = "Geçersiz token." };
        }

        public override IdentityError LoginAlreadyAssociated()
        {
            return new IdentityError { Code = nameof(LoginAlreadyAssociated), Description = "Bu kullanıcıyla zaten bir oturum açma işlemi ilişkilendirilmiş." };
        }

        public override IdentityError InvalidUserName(string userName)
        {
            return new IdentityError { Code = nameof(InvalidUserName), Description = $"Kullanıcı adı '{userName}' geçersiz, yalnızca harf veya rakam içerebilir." };
        }

        public override IdentityError InvalidEmail(string email)
        {
            return new IdentityError { Code = nameof(InvalidEmail), Description = $"'{email}' geçersiz bir e-posta adresi." };
        }

        public override IdentityError DuplicateUserName(string userName)
        {
            return new IdentityError { Code = nameof(DuplicateUserName), Description = $"'{userName}' kullanıcı adı zaten alınmış." };
        }

        public override IdentityError DuplicateEmail(string email)
        {
            return new IdentityError { Code = nameof(DuplicateEmail), Description = $"'{email}' e-posta adresi zaten kullanımda." };
        }

        public override IdentityError InvalidRoleName(string role)
        {
            return new IdentityError { Code = nameof(InvalidRoleName), Description = $"'{role}' geçersiz bir rol adı." };
        }

        public override IdentityError DuplicateRoleName(string role)
        {
            return new IdentityError { Code = nameof(DuplicateRoleName), Description = $"'{role}' rol adı zaten alınmış." };
        }

        public override IdentityError UserAlreadyHasPassword()
        {
            return new IdentityError { Code = nameof(UserAlreadyHasPassword), Description = "Kullanıcının zaten bir parolası var." };
        }

        public override IdentityError UserLockoutNotEnabled()
        {
            return new IdentityError { Code = nameof(UserLockoutNotEnabled), Description = "Bu kullanıcı için kilitlenme etkin değil." };
        }

        public override IdentityError UserAlreadyInRole(string role)
        {
            return new IdentityError { Code = nameof(UserAlreadyInRole), Description = $"Kullanıcı zaten '{role}' rolüne sahip." };
        }

        public override IdentityError UserNotInRole(string role)
        {
            return new IdentityError { Code = nameof(UserNotInRole), Description = $"Kullanıcı '{role}' rolüne sahip değil." };
        }

        public override IdentityError PasswordTooShort(int length)
        {
            return new IdentityError { Code = nameof(PasswordTooShort), Description = $"Parola en az {length} karakter uzunluğunda olmalıdır." };
        }

        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return new IdentityError { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Parola en az bir alfanümerik olmayan karakter içermelidir (!, *, vb.)." };
        }

        public override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError { Code = nameof(PasswordRequiresDigit), Description = "Parola en az bir rakam ('0'-'9') içermelidir." };
        }

        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError { Code = nameof(PasswordRequiresLower), Description = "Parola en az bir küçük harf ('a'-'z') içermelidir." };
        }

        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError { Code = nameof(PasswordRequiresUpper), Description = "Parola en az bir büyük harf ('A'-'Z') içermelidir." };
        }
    }
}

