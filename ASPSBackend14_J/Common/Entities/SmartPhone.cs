using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

public class SmartPhone : UserDevice
{
    // PhoneNumber inherited from UserDevice base class
    
    [NotMapped]
    public override string TypeName => "SmartPhone";
}
