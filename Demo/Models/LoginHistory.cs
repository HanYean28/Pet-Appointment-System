using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace PawfectGrooming.Models;

    public class LoginHistory
    {
        public int Id { get; set; }
        [ForeignKey("User")]
        public string Email { get; set; }
        public User User { get; set; }
        public DateTime LoginTime { get; set; }
        public string Devices { get; set; }
    }

