using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitTemplate.Application
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class EntryPoint : IExternalCommand
    {
        public static UIDocument uiDoc { get; set; }
        public static Document doc { get; set; }
        public static Autodesk.Revit.ApplicationServices.Application app { get; set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            app = commandData.Application.Application;
            uiDoc = commandData.Application.ActiveUIDocument;
            doc = uiDoc.Document;

            //ShowAllFamilyTypes(doc);
            PopulateSNVParameters(doc);
            return Result.Succeeded;
        }

        private void ShowAllFamilyTypes(Document doc)
        {
            // Get all FamilyInstance elements
            var cableTrayFittings = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                .Cast<FamilyInstance>()
                .ToList();

            // Select the collected elements in the UI
            var ids = cableTrayFittings.Select(e => e.Id).ToList();
            uiDoc.Selection.SetElementIds(ids);


            // Use a HashSet to avoid duplicates
            var typeInfoSet = new HashSet<string>();

            foreach (var instance in cableTrayFittings)
            {
                FamilySymbol type = instance.Symbol;
                Family family = type.Family;
                string info = $"Family: {family.Name}, Type: {type.Name}";
                typeInfoSet.Add(info);
            }

            // Build message
            var message = string.Join(Environment.NewLine, typeInfoSet.OrderBy(s => s));

            // Show in TaskDialog (limited size)
            TaskDialog.Show("Family Types in Model",
                string.IsNullOrWhiteSpace(message) ? "No family instances found." : message);

            TaskDialog.Show("Cable Tray Fittings", $"Total {ids.Count} cable tray fittings.");

        }

        private void PopulateSNVParameters(Document doc)
        {
            var cableTrayFittings = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                .Cast<FamilyInstance>()
                .ToList();

            using (Transaction tx = new Transaction(doc, "Populate SNV Parameters"))
            {
                tx.Start();

                foreach (FamilyInstance fi in cableTrayFittings)
                {
                    string familyName = fi.Symbol.Family.Name.ToLower();
                    string typeName = fi.Symbol.Name.ToLower();

                    string bendType = "";
                    string bendDetails = "";

                    // Determine Bend Type and Details
                    if (familyName.Contains("splice plate"))
                    {
                        bendType = "Splice Plate";
                        bendDetails = "";
                    }
                    else if (familyName.Contains("tee"))
                    {
                        bendType = "Tee";
                        bendDetails = "";
                    }
                    else if (familyName.Contains("reducer"))
                    {
                        bendType = "Reducer";
                        bendDetails = "";
                    }
                    else if (familyName.Contains("vertical"))
                    {
                        bendType = "Vertical";
                        if (familyName.Contains("inside"))
                            bendDetails = "Inside";
                        else if (familyName.Contains("outside"))
                            bendDetails = "Outside";
                    }
                    else if (familyName.Contains("horizontal"))
                    {
                        bendType = "Horizontal";
                        bendDetails = "";
                    }

                    // Set SNV_Bend_Type
                    Parameter bendTypeParam = fi.LookupParameter("SNV_Bend_Type");
                    if (bendTypeParam != null && bendTypeParam.StorageType == StorageType.String && !bendTypeParam.IsReadOnly)
                    {
                        try { bendTypeParam.Set(bendType); }
                        catch { TaskDialog.Show("Error", $"Failed to set SNV_Bend_Type for {fi.Id}"); }
                    }

                    // Set SNV_Bend_Details
                    Parameter bendDetailsParam = fi.LookupParameter("SNV_Bend_Details");
                    if (bendDetailsParam != null && bendDetailsParam.StorageType == StorageType.String && !bendDetailsParam.IsReadOnly)
                    {
                        try { bendDetailsParam.Set(bendDetails); }
                        catch { TaskDialog.Show("Error", $"Failed to set SNV_Bend_Details for {fi.Id}"); }
                    }

                    // Get 'Angle' value from the family type (shared parameter or family param)
                    Parameter angleParam = fi.LookupParameter("Angle");
                    if (angleParam != null && angleParam.StorageType == StorageType.Double)
                    {
                        double radians = angleParam.AsDouble();
                        double degrees = radians * (180.0 / Math.PI);
                        string angleText = Math.Round(degrees, 2).ToString("0.##");

                        var snvAngle = fi.LookupParameter("SNV_Angle");
                        if (snvAngle != null && !snvAngle.IsReadOnly)
                            snvAngle.Set(angleText);
                    }

                }

                tx.Commit();
            }

            TaskDialog.Show("Done", "SNV parameters populated from family/type names.");
        }

    }
}
