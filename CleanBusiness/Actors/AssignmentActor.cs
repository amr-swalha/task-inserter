using Akka.Actor;
using CleanBase.Dtos;

namespace CleanBusiness.Actors;

public class AssignmentActor : UntypedActor
{
    protected override void OnReceive(object message)
    {
        if (message is WorkerZoneAssignmentDto)
        {
            
        }
    }
}