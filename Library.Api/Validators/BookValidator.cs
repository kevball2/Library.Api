using FluentValidation;
using FluentValidation.Validators;
using Library.Api.Models;

namespace Library.Api.Validators
{
    public class BookValidator : AbstractValidator<Book>
    {
        public BookValidator() 
        {
            RuleFor(Book => Book.Isbn)
                .Matches(@"^(?=(?:\D*\d){3}-(?:(?:\D*\d){9})?$)[\d-]+$")
                .WithMessage("Value was not a valid ISBN-13");

            RuleFor(Book => Book.Title).NotEmpty();
            RuleFor(Book => Book.ShortDescription).NotEmpty();
            RuleFor(Book => Book.PageCount).GreaterThan(0);
            RuleFor(Book => Book.Author).NotEmpty();
        }
    }
}
