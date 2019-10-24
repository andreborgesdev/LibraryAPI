using AutoMapper;
using Library.API.Services;
using Library.API.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Library.API.Entities;
using Microsoft.AspNetCore.JsonPatch;
using Library.API.Helpers;
using Microsoft.Extensions.Logging;

namespace Library.API.Controllers
{
    [Route("api/authors/{authorId}/books")]
    public class BooksController : Controller
    {
        private ILibraryRepository _libraryRepository;
        private readonly IMapper _mapper;
        private ILogger<BooksController> _logger;
        private IUrlHelper _urlHelper;

        public BooksController(ILibraryRepository libraryRepository, 
            IMapper mapper, 
            ILogger<BooksController> logger,
            IUrlHelper urlHelper)
        {
            _libraryRepository = libraryRepository;
            _mapper = mapper;
            _logger = logger;
            _urlHelper = urlHelper;
        }

        [HttpGet(Name = "GetBooksForAuthor")]
        public IActionResult GetBooksForAuthor(Guid authorId)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var booksForAuthorFromRepo = _libraryRepository.GetBooksForAuthor(authorId);

            var booksForAuthor = _mapper.Map<IEnumerable<BookDto>>(booksForAuthorFromRepo);

            booksForAuthor = booksForAuthor.Select(book =>
            {
                book = CreateLinksForBook(book);
                return book;
            });

            var wrapper = new LinkedCollectionResourceWrapperDto<BookDto>(booksForAuthor);

            return Ok(CreateLinksForBooks(wrapper));
        }

        [HttpGet("{bookId}", Name = "GetBookForAuthor")]
        public IActionResult GetBookForAuthor(Guid authorId, Guid bookId)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, bookId);

            if (bookForAuthorFromRepo == null)
            {
                return NotFound();
            }

            var bookForAuthor = _mapper.Map<BookDto>(bookForAuthorFromRepo);

            return Ok(CreateLinksForBook(bookForAuthor));
        }

        [HttpPost(Name = "CreateBookForAuthor")]
        public IActionResult CreateBookForAuthor(Guid authorId, [FromBody] BookForCreationDto book)
        {
            if (book == null)
            {
                return BadRequest();
            }

            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookForCreationDto),
                    "The provided description should be different from the title");
            }

            if (!ModelState.IsValid)
            {
                //return 422
                return new UnprocessableEntityObjectResult(ModelState);
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookEntity = _mapper.Map<Book>(book);

            _libraryRepository.AddBookForAuthor(authorId, bookEntity);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Creating a book for author {authorId} failed on save.");
            }

            var bookToReturn = _mapper.Map<BookDto>(bookEntity);

            return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, bookId = bookToReturn.Id }
            , CreateLinksForBook(bookToReturn));
        }

        [HttpDelete("{bookId}", Name = "DeleteBookForAuthor")]
        public IActionResult DeleteBookForAuthor(Guid authorId, Guid bookId)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, bookId);

            if (bookForAuthorFromRepo == null)
            {
                return NotFound();
            }

            _libraryRepository.DeleteBook(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Deleting book {bookId} for author {authorId} failed on save");
            }

            _logger.LogInformation(100, $"Book {bookId} for author {authorId} was deleted.");

            return NoContent();
        }

        [HttpPut("{bookId}", Name = "UpdateBookForAuthor")]
        public IActionResult UpdateBookForAuthor(Guid authorId, Guid bookId, 
            [FromBody] BookForUpdateDto book)
        {
            if (book == null)
            {
                return BadRequest();
            }

            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookForCreationDto),
                    "The provided description should be different from the title");
            }

            if (!ModelState.IsValid)
            {
                //return 422
                return new UnprocessableEntityObjectResult(ModelState);
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, bookId);

            if (bookForAuthorFromRepo == null)
            {
                var bookToAdd = _mapper.Map<Book>(book);
                bookToAdd.Id = bookId;

                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);

                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {bookId} for author {authorId} failed on upsert.");
                }

                var bookToReturn = _mapper.Map<BookDto>(bookToAdd);

                return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, bookId = bookToReturn.Id }
                , bookToReturn);
            }

            _mapper.Map(book, bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Updating book {bookId} for author {authorId} failed on save.");
            }

            return NoContent();
        }

        [HttpPatch("{bookId}", Name = "PartiallyUpdateBookForAuthor")]
        public IActionResult PartiallyUpdateBookForAuthor(Guid authorId, Guid bookId,
            [FromBody] JsonPatchDocument<BookForUpdateDto> patchdoc)
        {
            if (patchdoc == null)
            {
                return BadRequest();
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, bookId);

            if (bookForAuthorFromRepo == null)
            {
                var bookDto = new BookForUpdateDto();
                patchdoc.ApplyTo(bookDto, ModelState);

                if (bookDto.Description == bookDto.Title)
                {
                    ModelState.AddModelError(nameof(BookForUpdateDto),
                        "The provided description should be different from the title.");
                }

                TryValidateModel(bookDto);

                if (!ModelState.IsValid)
                {
                    return new UnprocessableEntityObjectResult(ModelState);
                }

                var bookToAdd = _mapper.Map<Book>(bookDto);

                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);
                bookToAdd.Id = bookId;

                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {bookId} for author {authorId} failed on upsert.");
                }

                var bookToReturn = _mapper.Map<BookDto>(bookToAdd);

                return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, bookId = bookToReturn.Id }
                , bookToReturn);
            }

            var bookToPatch = _mapper.Map<BookForUpdateDto>(bookForAuthorFromRepo);

            patchdoc.ApplyTo(bookToPatch, ModelState);

            if (bookToPatch.Description == bookToPatch.Title)
            {
                ModelState.AddModelError(nameof(BookForUpdateDto),
                    "The provided description should be different from the title.");
            }

            TryValidateModel(bookToPatch);

            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            _mapper.Map(bookToPatch, bookForAuthorFromRepo);

            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Patching book {bookId} for author {authorId} failed on save.");
            }

            return NoContent();
        }

        private BookDto CreateLinksForBook(BookDto book)
        {
            book.Links.Add(new LinkDto(_urlHelper.Link("GetBookForAuthor",
                new { id = book.Id }),
                "self",
                "GET"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("DeleteBookForAuthor",
                new { id = book.Id }),
                "delete_book",
                "DELETE"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("UpdateBookForAuthor",
                new { id = book.Id }),
                "update_book",
                "PUT"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("PartiallyUpdateBookForAuthor",
                new { id = book.Id }),
                "partially_update_book",
                "PATCH"));

            return book;
        }

        private LinkedCollectionResourceWrapperDto<BookDto> CreateLinksForBooks(
            LinkedCollectionResourceWrapperDto<BookDto> booksWrapper)
        {
            // link to self
            booksWrapper.Links.Add(
                new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { }),
                "self",
                "GET"));

            return booksWrapper;
        }
    }
}
