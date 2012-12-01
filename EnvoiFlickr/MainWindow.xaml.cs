using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Windows.Threading;
using FlickrNet;

namespace EnvoiFlickr
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Déclaration des constantes
        public const String clefFlickr = "caafdbc81b5242ce72359f183fa97414";
        public const String secretFlickr = "97624892c344cba6";

        // Déclaration des variables
        public Flickr conFlickr = new Flickr(clefFlickr, secretFlickr);
        public Auth autFlickr = new Auth();
        public String tempFrob;
        public string[] extensions = { "*.jp*g", "*.gif", "*.png", "*.tif*", 
                                       "*.avi", "*.wmv", "*.mov", "*.mp*g*", 
                                       "*.3gp", "*.m2ts", "*.ogg", "*.ogv" };
        List<String> listeImages = new List<String>();
        List<String> listeAlbums = new List<String>();
        String idAlbumSelect;

        public MainWindow()
        {
            InitializeComponent();

            // Charge le dossier Images par défaut
            txtDossier.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        private void btnSuivant_Click(object sender, RoutedEventArgs e)
        {
            // Change d'onglet
            tbcAssistant.SelectedIndex++;
            changeOnglet(tbcAssistant);
        }

        private void btnPrecedent_Click(object sender, RoutedEventArgs e)
        {
            // Change d'onglet
            tbcAssistant.SelectedIndex--;
            changeOnglet(tbcAssistant);
        }

        private void changeOnglet(object sender)
        {
            // Active si besoin le bouton "Précédent"
            btnPrecedent.IsEnabled = tbcAssistant.SelectedIndex > 0;

            // Active si besoin le bouton "Suivant"
            btnSuivant.IsEnabled = (tbcAssistant.SelectedIndex < tbcAssistant.Items.Count - 1);

            // Change le texte du label
            lblEtape.Content = String.Format("Étape n°{0}", tbcAssistant.SelectedIndex + 1);

            // Lance l'envoi lors du passage à l'onglet n°4
            if (tbcAssistant.SelectedIndex == 3)
            {
                envoiFlickr();
            }
        }

        private void btnConnexion_Click(object sender, RoutedEventArgs e)
        {
            // Récupère le Frob pour l'autentification à Flickr
            tempFrob = conFlickr.AuthGetFrob();

            // Récupère l'URI pour la connexion à Flickr
            String flickrURI = conFlickr.AuthCalcUrl(tempFrob, AuthLevel.Write);

            // Lance le navigateur pour l'autorisation
            System.Diagnostics.Process.Start(flickrURI);

            // Active le bouton de validation
            btnValidation.IsEnabled = true;
            btnValidation.Focus();
        }

        private void btnValidation_Click(object sender, RoutedEventArgs e)
        {
            // Récupère l'autentification
            try
            {
                autFlickr = conFlickr.AuthGetToken(tempFrob);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Une erreur vient de ce produire :" + Environment.NewLine + ex.Message,
                    "Erreur d'autentification à Flickr", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Active le bouton "Suivant"
            btnSuivant.IsEnabled = true;

            // Affiche le quota restant
            if (conFlickr.PeopleGetUploadStatus().IsPro == false)
            {
                lblQuota.Content = String.Format("Quota restant sur votre compte Flickr : {0} Kio",
                    conFlickr.PeopleGetUploadStatus().BandwidthRemainingKB);
            }
            else
            {
                lblQuota.Content = "Quota restant sur votre compte Flickr : ∞ Kio";
            }

            // Récupère la liste des albums
            cmbAlbum.Items.Clear();
            listeAlbums.Clear();
            cmbAlbum.Items.Add("Sélectionnez un album");
            listeAlbums.Add("-1");
            cmbAlbum.Items.Add("Ajoutez un nouvel album…");
            listeAlbums.Add("0");
            PhotosetCollection lstAlbums = conFlickr.PhotosetsGetList();
            foreach (Photoset album in lstAlbums)
            {
                cmbAlbum.Items.Add(album.Title);
                listeAlbums.Add(album.PhotosetId);
            }
        }

        private void btnDossier_Click(object sender, RoutedEventArgs e)
        {
            // Affiche la boîte de dialogue de sélection d'un dossier
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Sélectionnez le dossier contenant les photos à envoyer sur Flickr :";
            dialog.ShowNewFolderButton = false;
            dialog.SelectedPath = txtDossier.Text;
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Récupère le chemin du dossier
                txtDossier.Text = dialog.SelectedPath;
            }
        }

        private void chkTitre_Checked(object sender, RoutedEventArgs e)
        {
            // Active la saisie du titre si besoin
            txtTitre.IsEnabled = chkTitre.IsChecked.Value;
        }

        private void chkDescription_Checked(object sender, RoutedEventArgs e)
        {
            // Active la saisie de la description
            txtDescription.IsEnabled = chkDescription.IsChecked.Value;
        }

        private void chkPosition_Checked(object sender, RoutedEventArgs e)
        {
            // Active les champs de saisie de la position si besoin
            lblLatitude.IsEnabled = chkPosition.IsChecked.Value;
            txtLatitude.IsEnabled = chkPosition.IsChecked.Value;
            lblDegreeLatitude.IsEnabled = chkPosition.IsChecked.Value;
            lblLongitude.IsEnabled = chkPosition.IsChecked.Value;
            txtLongitude.IsEnabled = chkPosition.IsChecked.Value;
            lblDegreeLongitude.IsEnabled = chkPosition.IsChecked.Value;
        }

        private void envoiFlickr()
        {
            // Désactive le bouton Précédent
            btnPrecedent.IsEnabled = false;

            // Vide la liste des images à envoyer
            lbEnvoi.Items.Clear();
            listeImages.Clear();

            // Réinitialise les barres de progression
            pbProgressionPhoto.Value = 0;
            pbProgressionTotal.Value = 0;

            // Parcours le dossier sélectionné
            parcoursRepertoire(txtDossier.Text);

            // Remplit la liste
            foreach (String fichier in listeImages)
            {
                lbEnvoi.Items.Add(fichier);
            }

            // Spécifie les limites de la barre de progression total
            pbProgressionTotal.Maximum = listeImages.Count();


            // Récupère le titre des images
            String tempTitre = String.Empty;
            if (chkTitre.IsChecked.Value)
            {
                tempTitre = txtTitre.Text;
            }

            // Récupère la description des images
            String tempDescription = String.Empty;
            if (chkDescription.IsChecked.Value)
            {
                tempDescription = txtDescription.Text;
            }

            // Initialise les variables
            Boolean isPublic = false;
            Boolean isFriend = false;
            Boolean isFamily = false;
            String photoId = String.Empty;

            // Récupère la visibilité des images
            switch (cmbVisibilite.SelectedIndex)
            {
                // Tout le monde
                case 0:
                    isPublic = true;
                    isFriend = true;
                    isFamily = true;
                    break;
                // Votre famille
                case 1:
                    isPublic = false;
                    isFriend = false;
                    isFamily = true;
                    break;
                // Vos amis
                case 2:
                    isPublic = false;
                    isFriend = true;
                    isFamily = false;
                    break;
                // Votre famille et vos amis
                case 3:
                    isPublic = false;
                    isFriend = false;
                    isFamily = true;
                    break;
                // Uniquement vous
                case 4:
                    isPublic = false;
                    isFriend = false;
                    isFamily = false;
                    break;
            }

            // Paramètre la fonction gérant la progression de l'envoi
            // TODO : Problème lors de la mise à jour de la barre de progression via le thread :
            // à corriger !
            //conFlickr.OnUploadProgress += flickrEnvoi;

            // Envoi chaque image
            int i = 0;

            foreach (String image in listeImages)
            {
                // Sélectionne l'image dans la liste
                lbEnvoi.SelectedIndex = i;
                pbProgressionTotal.Value = i;
                i++;

                try
                {
                    // Créer le Thread d'envoi
                    // Problèmes d'accès lors de l'exécution du thread :
                    // à corriger !
                    /*
                    Thread threadEnvoi = new Thread(delegate()
                    {
                        // Envoi de l'image sur Flickr
                        photoId = conFlickr.UploadPicture(image, tempTitre, tempDescription, String.Empty,
                            isPublic, isFamily, isFriend);

                        // Si la demande d'ajout à un album est faite
                        if (idAlbumSelect != "-1")
                        {
                            if (idAlbumSelect == "0")
                            {
                                // S'il s'agit d'un nouvel album : le créer
                                idAlbumSelect = conFlickr.PhotosetsCreate(txtNomAlbum.Text, photoId).PhotosetId;
                            }
                            else
                            {
                                // Sinon, ajouter la photo à l'album existant
                                conFlickr.PhotosetsAddPhoto(idAlbumSelect, photoId);
                            }
                        }

                        // Si la demande de géolocalisation est spécifiée
                        if (chkPosition.IsChecked.Value)
                        {
                            conFlickr.PhotosGeoSetLocation(photoId, Convert.ToDouble(txtLatitude.Text),
                                Convert.ToDouble(txtLongitude.Text));
                        }
                    });

                    // Lance le Thread
                    threadEnvoi.Start();
                    */
                }
                catch (FlickrException Ex)
                {
                    MessageBox.Show("Une erreur c'est produite lors de l'envoi de l'image vers Flickr."
                        + Environment.NewLine + Ex.Message, "Erreur d'envoi d'une image sur Flickr");
                }
                catch (Exception Ex)
                {
                    MessageBox.Show("Une super erreur c'est produite lors de l'envoi de l'image vers Flickr."
                        + Environment.NewLine + Ex.Message, "Super erreur d'envoi d'une image sur Flickr");
                }
            }
        }

        private void parcoursRepertoire(String repertoire)
        {
            // Récupère les fichiers du répertoire
            foreach (String extension in extensions)
            {
                foreach (String fichierCourant in System.IO.Directory.GetFiles(repertoire, extension))
                {
                    listeImages.Add(fichierCourant);
                }
            }

            // Parcours si besoin les sous répertoires
            if (chkRecrusive.IsChecked.Value)
            {
                foreach (String sousDossier in System.IO.Directory.GetDirectories(repertoire))
                {
                    parcoursRepertoire(sousDossier);
                }
            }
        }

        private void flickrEnvoi(object sender, UploadProgressEventArgs progression)
        {
            // Change la valeur de la barre de progression
            pbProgressionPhoto.Value = progression.ProcessPercentage;
        }

        private void cmbAlbum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si la liste n'est pas vide
            if (listeAlbums.Count != 0)
            {
                // Affiche si besoin le champ de saisie du nom du nouvel album
                if (listeAlbums[cmbAlbum.SelectedIndex] == "0")
                {
                    txtNomAlbum.Visibility = System.Windows.Visibility.Visible;
                    txtNomAlbum.Focus();
                }
                else
                {
                    txtNomAlbum.Visibility = System.Windows.Visibility.Hidden;
                }

                // Récupère l'id de l'album sélectionné
                idAlbumSelect = listeAlbums[cmbAlbum.SelectedIndex];
            }
        }
    }
}
