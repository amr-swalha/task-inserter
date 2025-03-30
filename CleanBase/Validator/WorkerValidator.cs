using CleanBase.Dtos;
using FluentValidation;

namespace CleanBase.Validator;

public class WorkerValidator : AbstractValidator<WorkerZoneAssignmentFileDto>
{
    public WorkerValidator()
    {
        RuleFor(x => x.Zone_Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Worker_Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Assignment_Date).Matches("^\\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
    }
}