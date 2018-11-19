using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web.Mvc;
using Dinamico.Models;
using N2;
using N2.Web.Mail;
using N2.Web.Mvc;
using N2.Web.Rendering;

namespace Dinamico.Controllers
{
	/// <summary>
	///     This controller is connected to it's content <see cref="FreeForm" /> via a
	///     fluent registration at <see cref="Registrations.FreeFormRegistration" />.
	/// </summary>
	public class FreeFormController : ContentController<FreeForm>
	{
		private readonly IMailSender mailSender;

		public FreeFormController(IMailSender mailSender)
		{
			this.mailSender = mailSender;
		}

		public override ActionResult Index()
		{
			bool formSubmit;
			if (bool.TryParse(Convert.ToString(TempData["FormSubmit"]), out formSubmit) && formSubmit)
				return PartialView("Submitted");

			return CurrentItem.GetTokens("Form").Any(dt => dt.Is("FormSubmit")) ? PartialView("Form") : PartialView();
		}

		public ActionResult Submit(FormCollection collection)
		{
			var mm = new MailMessage(CurrentItem.MailFrom, CurrentItem.MailTo.Replace(";", ","));
			mm.Subject = CurrentItem.MailSubject;
			mm.Headers["X-FreeForm-Submitter-IP"] = Request.UserHostName;
			mm.Headers["X-FreeForm-Submitter-Date"] = Utility.CurrentTime().ToString();
			using (var sw = new StringWriter())
			{
				sw.WriteLine(CurrentItem.MailBody);
				foreach (
					var token in
					CurrentItem.GetTokens("Form").Where(dt => dt.Name.StartsWith("Form", StringComparison.InvariantCultureIgnoreCase)))
				{
					var components = token.GetComponents();
					var name = components[0];
					name = token.Value ?? token.GenerateInputName();

					if (token.Is("FormSubmit"))
					{
					}
					else if (token.Is("FormFile"))
					{
						name = token.Value ?? token.GenerateInputName();

						if (Request.Files[name] == null)
							continue;

						var postedFile = Request.Files[name];
						if (postedFile.ContentLength == 0)
							continue;

						var fileName = Path.GetFileName(postedFile.FileName);
						sw.WriteLine(name + ": " + fileName + " (" + postedFile.ContentLength/1024 + "kB)");
						mm.Attachments.Add(new Attachment(postedFile.InputStream, fileName, postedFile.ContentType));
					}
					else if (token.Is("FormInput") || token.Is("FormTextarea"))
					{
						name = token.Value ?? token.GenerateInputName();
						var value = collection[name];
						sw.WriteLine(name + ": " + value);
					}
					else
					{
						name = token.GetOptionalInputName(0, 1);
						var value = collection[name];
						sw.WriteLine(name + ": " + value);

						// Assumption: any custom form input tokens with type=email are to be added to the reply-to list.
						if (components.Length >= 2 && string.Equals(components[1], "email", StringComparison.InvariantCultureIgnoreCase)
							&& !string.IsNullOrWhiteSpace(value))
						{
							try
							{
								var address = new MailAddress(value);
								// Prevent values with spaces from being interpreted as valid (see https://stackoverflow.com/a/1374644)
								if (value == address.Address)
								{
									mm.ReplyToList.Add(address);
								}
							}
							catch (FormatException) { }
						}
					}
				}

				mm.Body = sw.ToString();
			}

			mailSender.Send(mm);

			TempData.Add("FormSubmit", true);

			return RedirectToParentPage();
		}
	}
}