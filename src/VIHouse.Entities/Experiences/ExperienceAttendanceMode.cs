namespace VIHouse.Entities.Experiences;

/// <summary>
/// How an experience can be attended. Mirrors the online/in-person split Seminars already carry,
/// because a member joining needs to say which they mean and the House needs to know who to expect
/// in the room.
/// </summary>
public enum ExperienceAttendanceMode
{
    InPerson,
    Online,
    Both,
}
