﻿using System.ComponentModel.DataAnnotations;

namespace SintefSecureBoilerplate.MVC.Models.Manage
{
    public class AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }
    }
}
