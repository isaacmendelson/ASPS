#nullable enable
using Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views 
{
    public class PersonalComputerView : UserDeviceView
    {
        public PersonalComputerView(UserDevice entity) : base(entity)
        {
        }
    }
}
