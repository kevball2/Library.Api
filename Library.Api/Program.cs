using Library.Api.Data;
using Library.Api.Models;
using Library.Api.Services;
using FluentValidation;
using Library.Api.Validators;
using FluentValidation.Results;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqliteConnectionFactory(
        builder.Configuration.GetValue<string>("Database:ConnectionString")));
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("books", async (Book book, IBookService bookService, 
    IValidator<Book> validator) =>
{

    var validationResult = await validator.ValidateAsync(book);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }

    var created = await bookService.CreateAsync(book);
    if(!created)
    {
        return Results.BadRequest(new List<ValidationFailure>
        {
            new ("Isbn", "A book with this ISBN-13 already exists")
        });
    }

    return Results.Created($"/books/{book.Isbn}", book);
});

app.MapPut("books/{isbn}", async (Book book, IBookService bookService,
    IValidator<Book> validator, string isbn) =>
{
    book.Isbn = isbn;
    var validationResult = await validator.ValidateAsync(book);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }

    var updated = await bookService.UpdateAsync(book);
    return updated ? Results.Ok(book) : Results.NotFound();

    
});

app.MapGet("books", async (IBookService BookService, string? searchTerm) =>
{
    if (searchTerm is not null && !string.IsNullOrWhiteSpace(searchTerm))
    {
        var matchedBooks = await BookService.SearchByTitleAsync(searchTerm);
        return Results.Ok(matchedBooks);
    }

    var books = await BookService.GetAllAsync();
    return Results.Ok(books);
});


app.MapGet("books/{isbn}", async (string isbn, IBookService BookService) =>
{
    var book = await BookService.GetByIsbnAsync(isbn);
    return book is not null ?  Results.Ok(book) : Results.NotFound();
});

app.MapDelete("books/{isbn}", async (string isbn, IBookService BookService) =>
{
    var deleted = await BookService.DeleteAsync(isbn);
     
    return deleted ? Results.NoContent() : Results.NotFound();
});

var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
await databaseInitializer.InitializeAsync();

app.Run();
