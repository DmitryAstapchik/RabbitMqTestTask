using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Consumer
{
    internal class ModuleInfo
    {
        [Key]
        public string ModuleCategoryId { get; set; }
        public string ModuleState { get; set; }
    }
}
