namespace UniCP.Models
{
    public class MailBody
    {
        public string dogrulamamail(string? ad,string link){

            string strBody = "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f5f6f8;padding:40px 0;font-family:Arial,Helvetica,sans-serif;\">\r\n  " +
                "<tr>\r\n      " +
                "<td align=\"center\">\r\n           " +
                "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff;border-radius:8px;box-shadow:0 4px 15px rgba(0,0,0,0.08);\">" +
                "<tr>" +
                "<td style=\"padding:40px 40px 10px 40px;text-align:center;\">" +
                "<h2 style=\"margin:0;color:#333;font-size:24px;\">E-Posta Doğrulama</h2>" +
                "               </td>" +
                "           </tr>" +
                "    <tr>" +
                "    <td style=\"padding:20px 40px;color:#555;font-size:15px;line-height:22px;\">\r\n       " +
                "                 Merhaba <strong>"+ ad+"</strong> <br><br>\r\n                " +
                "        Hesabınızı aktif hale getirmek için aşağıdaki butona tıklayarak e-posta adresinizi doğrulayın.\r\n   " +
                "</td>" +
                "</tr>" +
                "       <tr>" +
                "       <td align=\"center\" style=\"padding:30px 40px;\">" +
                "   <a href=\"" + link + "\"" +
                "   style=\"background:#ff7a18;" +
                "           background:linear-gradient(135deg,#ff7a18,#ffb347);" +
                "           color:#ffffff;" +
                "           text-decoration:none;" +
                "           padding:14px 34px;" +
                "           font-size:16px;" +
                "           border-radius:6px;" +
                "           display:inline-block;" +
                "           font-weight:bold;\">" +
                "           ✔ E-Postamı Doğrula" +
                "            </a>" +
                "        </td>" +
                "       </tr>" +
                "<tr>" +
                "<td style=\"background:#f0f0f0;padding:15px 40px;border-bottom-left-radius:8px;border-bottom-right-radius:8px;color:#888;font-size:12px;text-align:center;\">" +
                "Bu mail otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız." +
                "</td>" +
                "</tr>" +
                "</table>" +
                "</td>" +
                "</tr>" +
                "</table>";


            return strBody;
        }

        public string resetlememail(string? ad, string link)
        {

            string strBody = "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f5f6f8;padding:40px 0;font-family:Arial,Helvetica,sans-serif;\">\r\n  " +
                "<tr>\r\n      " +
                "<td align=\"center\">\r\n           " +
                "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff;border-radius:8px;box-shadow:0 4px 15px rgba(0,0,0,0.08);\">" +
                "<tr>" +
                "<td style=\"padding:40px 40px 10px 40px;text-align:center;\">" +
                "<h2 style=\"margin:0;color:#333;font-size:24px;\">Parola Sıfırlama</h2>" +
                "               </td>" +
                "           </tr>" +
                "    <tr>" +
                "    <td style=\"padding:20px 40px;color:#555;font-size:15px;line-height:22px;\">\r\n       " +
                "                 Merhaba <strong>" + ad + "</strong> <br><br>\r\n                " +
                "        Hesabınızı aktif hale getirmek için aşağıdaki butona tıklayarak şifrenizi sıfırlayabilirsiniz.\r\n   " +
                "</td>" +
                "</tr>" +
                "       <tr>" +
                "       <td align=\"center\" style=\"padding:30px 40px;\">" +
                "   <a href=\"" + link + "\"" +
                "   style=\"background:#ff7a18;" +
                "           background:linear-gradient(135deg,#ff7a18,#ffb347);" +
                "           color:#ffffff;" +
                "           text-decoration:none;" +
                "           padding:14px 34px;" +
                "           font-size:16px;" +
                "           border-radius:6px;" +
                "           display:inline-block;" +
                "           font-weight:bold;\">" +
                "           ✔ Şifremi Yenile" +
                "            </a>" +
                "        </td>" +
                "       </tr>" +
                "<tr>" +
                "<td style=\"background:#f0f0f0;padding:15px 40px;border-bottom-left-radius:8px;border-bottom-right-radius:8px;color:#888;font-size:12px;text-align:center;\">" +
                "Bu mail otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız." +
                "</td>" +
                "</tr>" +
                "</table>" +
                "</td>" +
                "</tr>" +
                "</table>";


            return strBody;
        }


        public string TemporaryPasswordEmail(string? ad, string temporaryPassword)
        {
            string strBody = "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f5f6f8;padding:40px 0;font-family:Arial,Helvetica,sans-serif;\">\r\n  " +
                "<tr>\r\n      " +
                "<td align=\"center\">\r\n           " +
                "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff;border-radius:8px;box-shadow:0 4px 15px rgba(0,0,0,0.08);\">" +
                "<tr>" +
                "<td style=\"padding:40px 40px 10px 40px;text-align:center;\">" +
                "<h2 style=\"margin:0;color:#333;font-size:24px;\">Geçici Şifre</h2>" +
                "               </td>" +
                "           </tr>" +
                "    <tr>" +
                "    <td style=\"padding:20px 40px;color:#555;font-size:15px;line-height:22px;\">\r\n       " +
                "                 Merhaba <strong>" + ad + "</strong> <br><br>\r\n                " +
                "        Şifre sıfırlama talebiniz üzerine hesabınız için geçici bir şifre oluşturulmuştur. <br>Lütfen bu şifre ile giriş yapıp, şifrenizi değiştiriniz.\r\n   " +
                "</td>" +
                "</tr>" +
                "       <tr>" +
                "       <td align=\"center\" style=\"padding:30px 40px;\">" +
                "           <div style=\"background:#f0f0f0; padding:15px 30px; border-radius:6px; font-size:24px; font-weight:bold; letter-spacing:2px; color:#333; display:inline-block;\">" +
                "               " + temporaryPassword +
                "           </div>" +
                "        </td>" +
                "       </tr>" +
                "<tr>" +
                "<td style=\"background:#f0f0f0;padding:15px 40px;border-bottom-left-radius:8px;border-bottom-right-radius:8px;color:#888;font-size:12px;text-align:center;\">" +
                "Bu mail otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız." +
                "</td>" +
                "</tr>" +
                "</table>" +
                "</td>" +
                "</tr>" +
                "</table>";

            return strBody;
        }
    }
}
