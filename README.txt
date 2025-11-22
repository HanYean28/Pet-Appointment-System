# Pet Appointment System

A simple **Pet Appointment System** built using **ASP.NET (C#)**. This system allows members to book pet appointments while administrators manage users, appointments, and system operations.

This README is kept **simple and clean** so it's easy for lecturers, testers, and teammates to follow.

---

## 🚀 Features

* Member & Admin login
* Admin registration (requires admin key)
* Book and manage pet appointments
* View appointment history
* Temporary login keys for demo/testing

---

## 📥 How to Run the Project (ASP.NET C#)

### **1. Clone the Repository**

```
git clone https://github.com/HanYean28/Pet-Appointment-System.git
```

### **2. Open in Visual Studio**

1. Open **Visual Studio**
2. Click **Open a Project or Solution**
3. Select the folder you cloned

### **3. Restore Dependencies**

Visual Studio will automatically restore NuGet packages.
If not:

```
Tools → NuGet Package Manager → Restore
```

### **4. Set Database Connection**

Check your connection string in:

```
appsettings.json
```

Ensure it matches your SQL Server settings.

### **5. Import Database**

1. Open **SQL Server Management Studio (SSMS)**
2. Create a new database (example: `PetAppointmentDB`)
3. Import the `.sql` found in the `/database/` folder

### **6. Run the System**

Press **F5** or click **Run** in Visual Studio.

---

## 🔑 Login Credentials (For Testing)

### 👤 **Member**

* Email: **[hanyean282@gmail.com](mailto:hanyean282@gmail.com)**
* Password: **123123**
* Temporary Login Key: **key7749**

### 🛠 **Admin**

* Email: **[m1@gmail.com](mailto:m1@gmail.com)**
* Password: **123456abc**
* Admin Registration Key: **admin029373**
* Temporary Login Key: **key8864**

---

## 📝 Notes

* Admin account **cannot be created** without the correct admin key.
* Temporary login keys are for testing only.

---

Thank you for using the Pet Appointment System! 🐶🐱
