﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Models
{
    public abstract class BookForManipulationDto
    {
        [Required(ErrorMessage = "You should fill out a title.")]
        [MaxLength(100, ErrorMessage = "The title should not have more than 100 characters")]
        public string Title { get; set; }
        [MaxLength(500, ErrorMessage = "The description should not have more than 500 characters")]
        public virtual string Description { get; set; }
    }
}
