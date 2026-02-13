using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;

namespace Common.Entities;

public class PersonalComputer : UserDevice
{
    public PersonalComputerType Type { get; set; }
    public string MotherboardSerial { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public int Timezone { get; set; }
    // OperatingSystem inherited from UserDevice base class
    
    [NotMapped]
    public override string TypeName => "PersonalComputer";
}
