using System.ComponentModel.DataAnnotations;

namespace ESS.Models
{
    public class Company
    {
        [Key]
        [Required]
        [StringLength(2)]
        public string CompCode { get; set; }

        [StringLength(50)]
        public string CompName { get; set; }
    }
}