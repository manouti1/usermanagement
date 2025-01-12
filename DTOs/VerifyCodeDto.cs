﻿using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class VerifyCodeDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Code { get; set; }
    }
}
