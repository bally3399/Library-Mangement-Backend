

using System.ComponentModel.DataAnnotations;

namespace fortunae.Service.DTOs
{
    public class UserDTO
    {
        public Guid Id { get; set; }
        [Required(ErrorMessage ="use a registered username")]
        public string? Username { get; set; }
        [Required(ErrorMessage = "Invalid user role, user can either be Admin or Member")]
        public string? Role { get; set; }
    }
}
