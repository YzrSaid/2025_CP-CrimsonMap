// dashboard_welcome.js

document.addEventListener("DOMContentLoaded", () => {
  const userNameSpan = document.getElementById("userName");

  // Retrieve user data from sessionStorage
  const currentUser = JSON.parse(sessionStorage.getItem("currentUser"));

  if (currentUser && currentUser.firstName) {
    // Trim and convert full name (in case it contains multiple words)
    const cleanedName = currentUser.firstName.trim().toLowerCase();

    // Capitalize each word (e.g., "john doe" -> "John Doe")
    const formattedName = cleanedName
      .split(" ")
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

    userNameSpan.textContent = `${formattedName}!`;
  } else {
    // Redirect to login if not logged in
    window.location.href = "/html/login.html";
  }
});
